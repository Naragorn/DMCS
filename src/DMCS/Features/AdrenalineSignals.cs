using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Step 4d: what the player notices while the adrenaline window (or an execution window) is open.
    /// - Ready sound: DMCS's own synthesis after Dark Messiah's "Adrenaline_Reached" (<see cref="ReadySound"/>), once when the meter fills.
    /// - Heartbeat: the game's clip (Catalog.gameData.heartBeatLoopAddress) on DMCS's own AudioSource; the game's own
    ///   heartbeat source belongs to its health display (CameraEffects).
    /// - Haptics: a lub-dub pulse in both hands (RagdollHand.HapticTick).
    /// - Weapon glow: the game's "HyperfocusHand" effect on each hand that holds something, placed as SkillHyperFocus does.
    /// - Vignette: Dark Messiah-style streaks (<see cref="VignetteFlipbook"/>) on a head-locked spherical cap in front of
    ///   the eyes, drawn additively with the game's "Universal Render Pipeline/Particles/Unlit" (harness probe 2026-11-17:
    ///   loaded, supported; the head camera's near plane is 0.02 m). It flares on the critical.
    /// </summary>
    public static class AdrenalineSignals
    {
        private const string Channel = "Adrenaline";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const string GlowEffectId = "HyperfocusHand";
        private const int SoundRate = 44100;
        private const int FrameSize = 256;
        private const int FrameCount = 25;
        private const float CapRadius = 0.1f;
        private const int CapSegments = 24;
        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.6f;
        private const float FlareSeconds = 0.6f;
        private const float FlareBoost = 1.5f;

        private static readonly SignalFader Fader = new SignalFader(FadeIn, FadeOut);
        private static readonly SignalFlare FlareLevel = new SignalFlare(FlareSeconds);
        private static readonly List<EffectInstance> Glows = new List<EffectInstance>();

        private static GameObject _audioHost;
        private static AudioSource _readySource;
        private static AudioSource _heartbeatSource;
        private static AudioClip _readyClip;
        private static bool _heartbeatLoading;
        private static GameObject _vignette;
        private static Material _material;
        private static Texture2D[] _frames;
        private static VignetteSpec _framesSpec;
        private static bool _shaderMissingLogged;
        private static bool _active;
        private static float _activeSince;
        private static int _shownFrame = -1;
        private static float _vignetteSize = AdrenalineSettings.VignetteSize.Default;
        private static string _vignetteStyle = AdrenalineStyles.DefaultVignette;
        private static string _readySoundStyle = AdrenalineStyles.DefaultSound;
        private static string _heartbeatSound = AdrenalineStyles.DefaultHeartbeat;
        private static string _readyClipStyle;
        private static string _heartbeatClipStyle;
        private static AudioClip _gameHeartbeat;
        private static AudioClip _dmHeartbeat;
        private static string _framesStyle;
        private static Task<byte[][]> _building;
        private static VignetteSpec _buildingSpec;
        private static float _buildStarted;
        private static readonly Dictionary<string, Task<float[]>> SoundTasks = new Dictionary<string, Task<float[]>>();

        /// <summary>Saved option values are applied before the first level loads; from then on a picked version may set its look.</summary>
        private static bool _optionsApplied;

        [ModOption(name = AdrenalineSettings.ReadySoundName, tooltip = "A sound when the meter is full (after Dark Messiah's).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 30)]
        public static bool ReadySoundOn { get; set; } = AdrenalineSettings.DefaultReadySound;

        [ModOption(name = AdrenalineSettings.ReadySoundStyleName, tooltip = "Which meter-full sound: DMCS 1–3 are the versions DMCS built (3 = newest), Dark Messiah = the original, read from your own Dark Messiah install (falls back to DMCS 3 when it is not found).", valueSourceName = nameof(SoundStyleValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 31)]
        public static string ReadySoundStyle
        {
            get => _readySoundStyle;
            set
            {
                _readySoundStyle = value;
                BeginDarkMessiahIfPicked(value);
            }
        }

        [ModOption(name = AdrenalineSettings.ReadySoundVolumeName, tooltip = "Volume of the meter-full sound.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 32)]
        [ModOptionSlider]
        public static float ReadySoundVolume { get; set; } = AdrenalineSettings.ReadySoundVolume.Default;

        [ModOption(name = AdrenalineSettings.HeartbeatName, tooltip = "A heartbeat while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 33)]
        public static bool HeartbeatOn { get; set; } = AdrenalineSettings.DefaultHeartbeat;

        [ModOption(name = AdrenalineSettings.HeartbeatSoundName, tooltip = "Which heartbeat: Blade & Sorcery's own, or Dark Messiah's fast one from your own Dark Messiah install.", valueSourceName = nameof(HeartbeatSoundValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 34)]
        public static string HeartbeatSound
        {
            get => _heartbeatSound;
            set
            {
                _heartbeatSound = value;
                BeginDarkMessiahIfPicked(value);
            }
        }

        [ModOption(name = AdrenalineSettings.HeartbeatVolumeName, tooltip = "Volume of the heartbeat.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 35)]
        [ModOptionSlider]
        public static float HeartbeatVolume { get; set; } = AdrenalineSettings.HeartbeatVolume.Default;

        [ModOption(name = AdrenalineSettings.HapticsName, tooltip = "A heartbeat pulse in both controllers while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 36)]
        public static bool HapticsOn { get; set; } = AdrenalineSettings.DefaultHaptics;

        [ModOption(name = AdrenalineSettings.HapticsStrengthName, tooltip = "Strength of the controller pulse.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 37)]
        [ModOptionSlider]
        public static float HapticsStrength { get; set; } = AdrenalineSettings.HapticsStrength.Default;

        [ModOption(name = AdrenalineSettings.WeaponGlowName, tooltip = "The hand holding your weapon glows while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 38)]
        public static bool WeaponGlowOn { get; set; } = AdrenalineSettings.DefaultWeaponGlow;

        [ModOption(name = AdrenalineSettings.VignetteName, tooltip = "Flickering red streaks at the edge of your view while the meter is full (Dark Messiah style).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 39)]
        public static bool VignetteOn { get; set; } = AdrenalineSettings.DefaultVignette;

        /// <summary>
        /// Which drawing: DMCS 1–3 (choosing one sets streaks, length and hue as that version was built) or Dark Messiah's
        /// own frames from the player's install. Saved values win on load: the look is only set when the player picks a version.
        /// </summary>
        [ModOption(name = AdrenalineSettings.VignetteStyleName, tooltip = "Which vignette: DMCS 1 flames, DMCS 2 streaks, DMCS 3 patchy streaks, DMCS 4 nearly as Dark Messiah (shimmers in place), Dark Messiah = the original frames from your own Dark Messiah install. Picking a DMCS version sets streaks, length and hue as it was built.", valueSourceName = nameof(VignetteStyleValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 40)]
        public static string VignetteStyle
        {
            get => _vignetteStyle;
            set
            {
                var changed = value != _vignetteStyle;
                _vignetteStyle = value;
                BeginDarkMessiahIfPicked(value);
                if (changed && _optionsApplied && !AdrenalineStyles.IsDarkMessiah(value))
                {
                    var (streaks, length, hue) = AdrenalineStyles.VignetteLook(value);
                    OptionSync.Apply(AdrenalineSettings.VignetteStreaksName, streaks);
                    OptionSync.Apply(AdrenalineSettings.VignetteLengthName, length);
                    OptionSync.Apply(AdrenalineSettings.VignetteHueName, hue);
                }
            }
        }

        [ModOption(name = AdrenalineSettings.VignetteIntensityName, tooltip = "How bright the streaks are.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 41)]
        [ModOptionSlider]
        public static float VignetteIntensity { get; set; } = AdrenalineSettings.VignetteIntensity.Default;

        [ModOption(name = AdrenalineSettings.VignetteHueName, tooltip = "Colour of the streaks: 0 = red, 40 = orange.", valueSourceName = nameof(HueValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 42)]
        [ModOptionSlider]
        public static float VignetteHue { get; set; } = AdrenalineSettings.VignetteHue.Default;

        [ModOption(name = AdrenalineSettings.VignetteStreaksName, tooltip = "How many streaks each frame has.", valueSourceName = nameof(StreakValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 43)]
        [ModOptionSlider]
        public static int VignetteStreaks { get; set; } = AdrenalineSettings.DefaultVignetteStreaks;

        [ModOption(name = AdrenalineSettings.VignetteLengthName, tooltip = "How far the streaks reach in from the edge.", valueSourceName = nameof(LengthValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 44)]
        [ModOptionSlider]
        public static float VignetteLength { get; set; } = AdrenalineSettings.VignetteLength.Default;

        [ModOption(name = AdrenalineSettings.VignetteSizeName, tooltip = "Degrees from the centre of view to where the streaks start. Smaller = they reach further into view.", valueSourceName = nameof(SizeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 45)]
        [ModOptionSlider]
        public static float VignetteSize
        {
            get => _vignetteSize;
            set
            {
                _vignetteSize = value;
                if (_vignette != null)
                {
                    UnityEngine.Object.Destroy(_vignette);
                    _vignette = null;
                }
            }
        }

        [ModOption(name = AdrenalineSettings.VignetteFrameRateName, tooltip = "How fast the streaks flicker (frames per second).", valueSourceName = nameof(FrameRateValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 46)]
        [ModOptionSlider]
        public static float VignetteFrameRate { get; set; } = AdrenalineSettings.VignetteFrameRate.Default;

        [ModOption(name = AdrenalineSettings.VignetteFlareName, tooltip = "The streaks flare up on the critical.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 47)]
        public static bool VignetteFlareOn { get; set; } = AdrenalineSettings.DefaultVignetteFlare;

        public static ModOptionString[] SoundStyleValues() => Strings(AdrenalineStyles.SoundNames);

        public static ModOptionString[] HeartbeatSoundValues() => Strings(AdrenalineStyles.HeartbeatNames);

        public static ModOptionString[] VignetteStyleValues() => Strings(AdrenalineStyles.VignetteNames);

        private static ModOptionString[] Strings(string[] names) => names.Select(n => new ModOptionString(n, null, n)).ToArray();

        public static ModOptionFloat[] VolumeValues() => Floats(AdrenalineSettings.ReadySoundVolume, "0.00", "");

        public static ModOptionFloat[] HueValues() => Floats(AdrenalineSettings.VignetteHue, "0", "°");

        public static ModOptionFloat[] LengthValues() => Floats(AdrenalineSettings.VignetteLength, "0.00", "");

        public static ModOptionFloat[] SizeValues() => Floats(AdrenalineSettings.VignetteSize, "0", "°");

        public static ModOptionFloat[] FrameRateValues() => Floats(AdrenalineSettings.VignetteFrameRate, "0", " fps");

        public static ModOptionInt[] StreakValues() =>
            AdrenalineSettings.VignetteStreakValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture), v)).ToArray();

        /// <summary>Level loaded: build the sound and the frames now, so the first full meter does not stutter.</summary>
        public static void Prewarm()
        {
            _optionsApplied = true;
            try
            {
                foreach (var name in new[] { AdrenalineStyles.Dmcs1, AdrenalineStyles.Dmcs2, AdrenalineStyles.Dmcs3 })
                {
                    SoundSamples(name);
                }
                if (VignetteOn)
                {
                    EnsureFrames();
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline signals error: " + ex);
            }
        }

        /// <summary>Dark Messiah's files are read in the background as soon as one of its options is picked.</summary>
        private static void BeginDarkMessiahIfPicked(string value)
        {
            if (AdrenalineStyles.IsDarkMessiah(value))
            {
                DarkMessiahAssets.Begin();
            }
        }

        /// <summary>A DMCS version's samples, synthesised once in the background (the ring alone takes a while in the game's Mono).</summary>
        private static Task<float[]> SoundSamples(string name)
        {
            if (!SoundTasks.TryGetValue(name, out var task))
            {
                var spec = AdrenalineStyles.Sound(name);
                task = Task.Run(() => ReadySound.Generate(SoundRate, 1, spec));
                SoundTasks[name] = task;
            }
            return task;
        }

        private static VignetteSpec CurrentSpec() =>
            new VignetteSpec(FrameSize, FrameCount, VignetteStreaks, VignetteLength, VignetteHue, AdrenalineStyles.Vignette(VignetteStyle));

        /// <summary>The ready clip of the chosen version (Dark Messiah's own when picked and found, else DMCS's synthesis).</summary>
        private static void EnsureReadyClip()
        {
            var style = ReadySoundStyle;
            if (_readyClip != null && _readyClipStyle == style)
            {
                return;
            }
            var dm = AdrenalineStyles.IsDarkMessiah(style) ? DarkMessiahAssets.ReadySound : null;
            var used = dm != null ? AdrenalineStyles.DarkMessiah : AdrenalineStyles.IsDarkMessiah(style) ? AdrenalineStyles.Dmcs3 : style;
            if (_readyClip != null)
            {
                UnityEngine.Object.Destroy(_readyClip);
            }
            if (dm != null)
            {
                _readyClip = Clip("DMCS Adrenaline Ready (Dark Messiah)", dm.Samples, dm.Channels, dm.Rate);
            }
            else
            {
                _readyClip = Clip("DMCS Adrenaline Ready (" + used + ")", SoundSamples(used).Result, 2, SoundRate);
            }
            _readyClip.name = used;
            _readyClipStyle = style;
        }

        private static AudioClip Clip(string name, float[] samples, int channels, int rate)
        {
            var clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>The meter just filled.</summary>
        public static void WindowOpened()
        {
            if (!ReadySoundOn)
            {
                return;
            }
            try
            {
                EnsureAudio();
                EnsureReadyClip();
                _readySource.PlayOneShot(_readyClip, ReadySoundVolume);
                Dmcs.Log.Debug(Channel, "signal: ready sound " + _readyClip.name + " (" + _readyClip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s)");
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline signals error: " + ex);
            }
        }

        /// <summary>The critical landed: the streaks flare once.</summary>
        public static void Flare()
        {
            if (VignetteOn && VignetteFlareOn)
            {
                FlareLevel.Trigger(Time.unscaledTime);
                Dmcs.Log.Debug(Channel, "signal: vignette flare");
            }
        }

        public static void Update(bool featureEnabled, bool adrenalineWindowOpen, bool executionWindowOpen)
        {
            try
            {
                var on = AdrenalineSignalRule.Active(featureEnabled, adrenalineWindowOpen, executionWindowOpen);
                if (on && !_active)
                {
                    Start();
                }
                else if (!on && _active)
                {
                    End();
                }
                var now = Time.unscaledTime;
                var level = Fader.Update(on, Time.unscaledDeltaTime);
                var flare = featureEnabled ? FlareLevel.Level(now) : 0f;
                if (VignetteOn)
                {
                    EnsureFrames();
                }
                UpdateHeartbeat(on, level);
                UpdateHaptics(on, now, level);
                UpdateVignette(now, level, flare);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline signals error: " + ex);
                StopAll();
            }
        }

        /// <summary>Feature off, level unload: everything stops at once.</summary>
        public static void StopAll()
        {
            if (_active)
            {
                Dmcs.Log.Debug(Channel, "signals off (stopped)");
            }
            _active = false;
            Fader.Reset();
            FlareLevel.Reset();
            EndGlows();
            if (_heartbeatSource != null)
            {
                _heartbeatSource.Stop();
            }
            if (_readySource != null)
            {
                _readySource.Stop();
            }
            if (_vignette != null)
            {
                _vignette.SetActive(false);
            }
        }

        private static void Start()
        {
            _active = true;
            _activeSince = Time.unscaledTime;
            if (WeaponGlowOn)
            {
                StartGlows();
            }
            Dmcs.Log.Debug(Channel, "signals on: heartbeat " + OnOff(HeartbeatOn) + ", haptics " + OnOff(HapticsOn)
                + ", glow " + Glows.Count + " hand(s), vignette " + OnOff(VignetteOn));
        }

        private static void End()
        {
            _active = false;
            EndGlows();
            Dmcs.Log.Debug(Channel, "signals off");
        }

        private static string OnOff(bool v) => v ? "on" : "off";

        private static void EnsureAudio()
        {
            if (_audioHost != null)
            {
                return;
            }
            _audioHost = new GameObject("DMCS Adrenaline Audio");
            UnityEngine.Object.DontDestroyOnLoad(_audioHost);
            _readySource = NewSource(false);
            _heartbeatSource = NewSource(true);
        }

        private static AudioSource NewSource(bool loop)
        {
            var source = _audioHost.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.loop = loop;
            source.ignoreListenerPause = false;
            return source;
        }

        private static void UpdateHeartbeat(bool on, float level)
        {
            var want = on && HeartbeatOn;
            if (!want && (_heartbeatSource == null || !_heartbeatSource.isPlaying))
            {
                return;
            }
            EnsureAudio();
            if (!EnsureHeartbeatClip())
            {
                return;
            }
            _heartbeatSource.volume = HeartbeatVolume * (HeartbeatOn ? level : 0f);
            if (want && !_heartbeatSource.isPlaying)
            {
                _heartbeatSource.Play();
                Dmcs.Log.Debug(Channel, "signal: heartbeat playing");
            }
            else if (!want && level <= 0f && _heartbeatSource.isPlaying)
            {
                _heartbeatSource.Stop();
            }
        }

        /// <summary>
        /// Puts the chosen heartbeat on the source: Dark Messiah's fast one (looped) when picked and found, else the game's
        /// own loop (loaded once from the catalog). False while the game's clip is still loading.
        /// </summary>
        private static bool EnsureHeartbeatClip()
        {
            var style = HeartbeatSound;
            if (_heartbeatSource.clip != null && _heartbeatClipStyle == style)
            {
                return true;
            }
            AudioClip clip = null;
            if (AdrenalineStyles.IsDarkMessiah(style))
            {
                if (!DarkMessiahAssets.TryHeartbeat(out var w))
                {
                    return false;
                }
                if (_dmHeartbeat == null && w != null)
                {
                    _dmHeartbeat = Clip("DMCS Heartbeat (Dark Messiah)", w.Samples, w.Channels, w.Rate);
                }
                clip = _dmHeartbeat;
            }
            if (clip == null)
            {
                if (_gameHeartbeat == null)
                {
                    if (!_heartbeatLoading)
                    {
                        _heartbeatLoading = true;
                        Catalog.LoadAssetAsync<AudioClip>(Catalog.gameData.heartBeatLoopAddress, loaded =>
                        {
                            _heartbeatLoading = false;
                            _gameHeartbeat = loaded;
                            Dmcs.Log.Debug(Channel, "signal: heartbeat clip " + (loaded == null ? "missing" : "loaded"));
                        }, "DMCS");
                    }
                    return false;
                }
                clip = _gameHeartbeat;
            }
            var playing = _heartbeatSource.isPlaying;
            _heartbeatSource.Stop();
            _heartbeatSource.clip = clip;
            _heartbeatClipStyle = style;
            Dmcs.Log.Debug(Channel, "signal: heartbeat " + (clip == _dmHeartbeat ? AdrenalineStyles.DarkMessiah : AdrenalineStyles.BladeAndSorcery));
            if (playing)
            {
                _heartbeatSource.Play();
            }
            return true;
        }

        private static void UpdateHaptics(bool on, float now, float level)
        {
            if (!on || !HapticsOn || HapticsStrength <= 0f)
            {
                return;
            }
            var pulse = HeartbeatPulse.At(now - _activeSince, AdrenalineSettings.HeartbeatBpm) * HapticsStrength * level;
            if (pulse <= 0.02f)
            {
                return;
            }
            var player = Player.currentCreature;
            if (player == null)
            {
                return;
            }
            player.handLeft?.HapticTick(pulse);
            player.handRight?.HapticTick(pulse);
        }

        private static void StartGlows()
        {
            EndGlows();
            var player = Player.currentCreature;
            var data = Catalog.GetData<EffectData>(GlowEffectId);
            if (player == null || data == null)
            {
                return;
            }
            foreach (var hand in new[] { player.handLeft, player.handRight })
            {
                if (hand == null || hand.grabbedHandle == null)
                {
                    continue;
                }
                var effect = data.Spawn(hand.transform.position + (hand.PointDir * 0.05f), Quaternion.LookRotation(hand.PalmDir), hand.transform,
                    null, true, null, false, 1f, 1f);
                if (effect != null)
                {
                    effect.Play();
                    Glows.Add(effect);
                }
            }
        }

        private static void EndGlows()
        {
            foreach (var g in Glows)
            {
                g?.End();
            }
            Glows.Clear();
        }

        private static void UpdateVignette(float now, float level, float flare)
        {
            var visible = VignetteOn && (level > 0f || flare > 0f);
            if (!visible)
            {
                if (_vignette != null && _vignette.activeSelf)
                {
                    _vignette.SetActive(false);
                }
                return;
            }
            if (!EnsureVignette())
            {
                return;
            }
            if (!_vignette.activeSelf)
            {
                _vignette.SetActive(true);
                _shownFrame = -1;
            }
            var frame = VignetteFlipbook.FrameAt(now - _activeSince, VignetteFrameRate, _frames.Length);
            if (frame != _shownFrame)
            {
                _material.SetTexture("_BaseMap", _frames[frame]);
                _shownFrame = frame;
            }
            var boost = 1f + (FlareBoost * flare);
            var alpha = Mathf.Clamp01(VignetteIntensity * Mathf.Max(level, flare));
            _material.SetColor("_BaseColor", new Color(boost, boost, boost, alpha));
        }

        /// <summary>Builds (or rebuilds after a slider change) the frames, the material and the head-locked cap.</summary>
        private static bool EnsureVignette()
        {
            var cam = Player.local != null && Player.local.head != null ? Player.local.head.cam : null;
            if (cam == null)
            {
                return false;
            }
            if (_material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    if (!_shaderMissingLogged)
                    {
                        Dmcs.Log.Info("Adrenaline vignette: shader '" + ShaderName + "' not found; vignette off.");
                        _shaderMissingLogged = true;
                    }
                    return false;
                }
                _material = NewAdditive(shader);
                Dmcs.Log.Debug(Channel, "signal: vignette shader " + shader.name);
            }
            EnsureFrames();
            if (_vignette == null || _vignette.transform.parent != cam.transform)
            {
                if (_vignette != null)
                {
                    UnityEngine.Object.Destroy(_vignette);
                }
                _vignette = NewCap(cam);
            }
            return _frames != null;
        }

        /// <summary>Additive transparent setup of URP Particles/Unlit, as its editor GUI would set it (URP ParticlesUnlit.shader).</summary>
        private static Material NewAdditive(Shader shader)
        {
            var m = new Material(shader) { name = "DMCS Adrenaline Vignette" };
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            return m;
        }

        /// <summary>
        /// Builds the frames again when the version or a look slider changed: drawn on a background thread (about 0.4 s in
        /// the game's Mono, measured 2026-11-17), uploaded when done; the old frames stay on screen meanwhile.
        /// </summary>
        private static void EnsureFrames()
        {
            var style = VignetteStyle;
            if (AdrenalineStyles.IsDarkMessiah(style))
            {
                if (_frames != null && _framesStyle == style && _framesSpec.Frames == 0)
                {
                    return;
                }
                if (!DarkMessiahAssets.TryVignette(out var dm))
                {
                    return;
                }
                if (dm != null)
                {
                    LoadFrames(dm);
                    _framesStyle = style;
                    _framesSpec = default;
                    return;
                }
            }
            var spec = CurrentSpec();
            if (_frames != null && _framesStyle == style && Same(spec, _framesSpec))
            {
                return;
            }
            if (_building != null)
            {
                if (!_building.IsCompleted)
                {
                    return;
                }
                var done = _building;
                var doneSpec = _buildingSpec;
                _building = null;
                if (done.IsFaulted)
                {
                    Dmcs.Log.Info("Adrenaline vignette: drawing failed: " + done.Exception?.GetBaseException().Message);
                    return;
                }
                if (Same(doneSpec, spec))
                {
                    Upload(done.Result, spec);
                    _framesStyle = style;
                    return;
                }
            }
            _buildingSpec = spec;
            _buildStarted = Time.realtimeSinceStartup;
            _building = Task.Run(() => VignetteFlipbook.Generate(spec, 1));
        }

        /// <summary>Dark Messiah's own frames (decoded top row first; Unity textures start at the bottom row).</summary>
        private static void LoadFrames(VtfData dm)
        {
            DestroyFrames();
            _frames = dm.Frames.Select((f, i) => Texture(VtfData.FlipRows(f, dm.Width, dm.Height), dm.Width, "DMCS Vignette DM " + i)).ToArray();
            _shownFrame = -1;
            Dmcs.Log.Debug(Channel, "signal: vignette Dark Messiah: " + _frames.Length + " frames loaded");
        }

        private static void DestroyFrames()
        {
            if (_frames == null)
            {
                return;
            }
            foreach (var t in _frames)
            {
                UnityEngine.Object.Destroy(t);
            }
            _frames = null;
        }

        private static Texture2D Texture(byte[] rgba, int size, string name)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name,
            };
            t.LoadRawTextureData(rgba);
            t.Apply(false, true);
            return t;
        }

        private static void Upload(byte[][] pixels, VignetteSpec spec)
        {
            DestroyFrames();
            _frames = pixels.Select((p, i) => Texture(p, spec.Size, "DMCS Vignette " + i)).ToArray();
            _framesSpec = spec;
            _shownFrame = -1;
            Dmcs.Log.Debug(Channel, "signal: vignette " + VignetteName(spec.Style) + ": " + pixels.Length + " frames built in "
                + ((Time.realtimeSinceStartup - _buildStarted) * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms (background)");
        }

        private static string VignetteName(VignetteStyle style) =>
            style == Core.VignetteStyle.Flames ? AdrenalineStyles.Dmcs1
            : style == Core.VignetteStyle.Streaks ? AdrenalineStyles.Dmcs2
            : style == Core.VignetteStyle.Embers ? AdrenalineStyles.Dmcs4
            : AdrenalineStyles.Dmcs3;

        private static bool Same(VignetteSpec a, VignetteSpec b) =>
            a.Size == b.Size && a.Frames == b.Frames && a.Streaks == b.Streaks
            && Mathf.Approximately(a.Length, b.Length) && Mathf.Approximately(a.HueDegrees, b.HueDegrees) && a.Style == b.Style;

        /// <summary>
        /// A piece of a sphere around the eyes, <see cref="VignetteSize"/> degrees to every side, with the texture spread
        /// evenly over the angle: the frame's edge sits that far off the view axis, so the streaks reach in from the edge of sight.
        /// </summary>
        private static GameObject NewCap(Camera cam)
        {
            var go = new GameObject("DMCS Adrenaline Vignette");
            go.transform.SetParent(cam.transform, false);
            go.layer = VisibleLayer(cam);
            var n = CapSegments + 1;
            var vertices = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            var colors = new Color[n * n];
            for (var y = 0; y < n; y++)
            {
                for (var x = 0; x < n; x++)
                {
                    var u = (float)x / CapSegments;
                    var v = (float)y / CapSegments;
                    var yaw = ((u * 2f) - 1f) * VignetteSize;
                    var pitch = ((v * 2f) - 1f) * VignetteSize;
                    vertices[(y * n) + x] = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward * CapRadius;
                    uvs[(y * n) + x] = new Vector2(u, v);
                    // The frames' corners are lit (Dark Messiah's too), so the cap's square border would show as a hard
                    // line; it fades out over the outer 15 % instead (the particle shader multiplies the vertex colour).
                    var border = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
                    colors[(y * n) + x] = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, border)));
                }
            }
            var triangles = new int[CapSegments * CapSegments * 6];
            var k = 0;
            for (var y = 0; y < CapSegments; y++)
            {
                for (var x = 0; x < CapSegments; x++)
                {
                    var i = (y * n) + x;
                    triangles[k++] = i;
                    triangles[k++] = i + n;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + 1;
                    triangles[k++] = i + n;
                    triangles[k++] = i + n + 1;
                }
            }
            var mesh = new Mesh { name = "DMCS Vignette Cap", vertices = vertices, uv = uvs, colors = colors, triangles = triangles };
            mesh.bounds = new Bounds(Vector3.forward * CapRadius, Vector3.one * CapRadius * 4f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Dmcs.Log.Debug(Channel, "signal: vignette cap on camera '" + cam.name + "', layer " + go.layer + ", "
                + VignetteSize.ToString("0", CultureInfo.InvariantCulture) + "° to the edge");
            return go;
        }

        /// <summary>The Default layer if the camera draws it, else the lowest layer it draws.</summary>
        private static int VisibleLayer(Camera cam)
        {
            if ((cam.cullingMask & 1) != 0)
            {
                return 0;
            }
            for (var i = 0; i < 32; i++)
            {
                if ((cam.cullingMask & (1 << i)) != 0)
                {
                    return i;
                }
            }
            return 0;
        }

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();
    }
}
