using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        [ModOption(name = AdrenalineSettings.ReadySoundName, tooltip = "A sound when the meter is full (after Dark Messiah's).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 30)]
        public static bool ReadySoundOn { get; set; } = AdrenalineSettings.DefaultReadySound;

        [ModOption(name = AdrenalineSettings.ReadySoundVolumeName, tooltip = "Volume of the meter-full sound.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 31)]
        [ModOptionSlider]
        public static float ReadySoundVolume { get; set; } = AdrenalineSettings.ReadySoundVolume.Default;

        [ModOption(name = AdrenalineSettings.HeartbeatName, tooltip = "A heartbeat while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 32)]
        public static bool HeartbeatOn { get; set; } = AdrenalineSettings.DefaultHeartbeat;

        [ModOption(name = AdrenalineSettings.HeartbeatVolumeName, tooltip = "Volume of the heartbeat.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 33)]
        [ModOptionSlider]
        public static float HeartbeatVolume { get; set; } = AdrenalineSettings.HeartbeatVolume.Default;

        [ModOption(name = AdrenalineSettings.HapticsName, tooltip = "A heartbeat pulse in both controllers while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 34)]
        public static bool HapticsOn { get; set; } = AdrenalineSettings.DefaultHaptics;

        [ModOption(name = AdrenalineSettings.HapticsStrengthName, tooltip = "Strength of the controller pulse.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 35)]
        [ModOptionSlider]
        public static float HapticsStrength { get; set; } = AdrenalineSettings.HapticsStrength.Default;

        [ModOption(name = AdrenalineSettings.WeaponGlowName, tooltip = "The hand holding your weapon glows while the meter is full.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 36)]
        public static bool WeaponGlowOn { get; set; } = AdrenalineSettings.DefaultWeaponGlow;

        [ModOption(name = AdrenalineSettings.VignetteName, tooltip = "Flickering red streaks at the edge of your view while the meter is full (Dark Messiah style).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 37)]
        public static bool VignetteOn { get; set; } = AdrenalineSettings.DefaultVignette;

        [ModOption(name = AdrenalineSettings.VignetteIntensityName, tooltip = "How bright the streaks are.", valueSourceName = nameof(VolumeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 38)]
        [ModOptionSlider]
        public static float VignetteIntensity { get; set; } = AdrenalineSettings.VignetteIntensity.Default;

        [ModOption(name = AdrenalineSettings.VignetteHueName, tooltip = "Colour of the streaks: 0 = red, 40 = orange.", valueSourceName = nameof(HueValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 39)]
        [ModOptionSlider]
        public static float VignetteHue { get; set; } = AdrenalineSettings.VignetteHue.Default;

        [ModOption(name = AdrenalineSettings.VignetteStreaksName, tooltip = "How many streaks each frame has.", valueSourceName = nameof(StreakValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 40)]
        [ModOptionSlider]
        public static int VignetteStreaks { get; set; } = AdrenalineSettings.DefaultVignetteStreaks;

        [ModOption(name = AdrenalineSettings.VignetteLengthName, tooltip = "How far the streaks reach in from the edge.", valueSourceName = nameof(LengthValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 41)]
        [ModOptionSlider]
        public static float VignetteLength { get; set; } = AdrenalineSettings.VignetteLength.Default;

        [ModOption(name = AdrenalineSettings.VignetteSizeName, tooltip = "Degrees from the centre of view to where the streaks start. Smaller = they reach further into view.", valueSourceName = nameof(SizeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 42)]
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

        [ModOption(name = AdrenalineSettings.VignetteFrameRateName, tooltip = "How fast the streaks flicker (frames per second).", valueSourceName = nameof(FrameRateValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 43)]
        [ModOptionSlider]
        public static float VignetteFrameRate { get; set; } = AdrenalineSettings.VignetteFrameRate.Default;

        [ModOption(name = AdrenalineSettings.VignetteFlareName, tooltip = "The streaks flare up on the critical.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 44)]
        public static bool VignetteFlareOn { get; set; } = AdrenalineSettings.DefaultVignetteFlare;

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
            try
            {
                EnsureReadyClip();
                if (VignetteOn)
                {
                    var spec = CurrentSpec();
                    if (_frames == null || !Same(spec, _framesSpec))
                    {
                        BuildFrames(spec);
                    }
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline signals error: " + ex);
            }
        }

        private static VignetteSpec CurrentSpec() => new VignetteSpec(FrameSize, FrameCount, VignetteStreaks, VignetteLength, VignetteHue);

        private static void EnsureReadyClip()
        {
            if (_readyClip != null)
            {
                return;
            }
            var samples = ReadySound.Generate(SoundRate, 1, new ReadySoundSpec());
            _readyClip = AudioClip.Create("DMCS Adrenaline Ready", samples.Length / 2, 2, SoundRate, false);
            _readyClip.SetData(samples, 0);
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
                Dmcs.Log.Debug(Channel, "signal: ready sound (" + _readyClip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s)");
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
            if (_heartbeatSource.clip == null)
            {
                if (!_heartbeatLoading && want)
                {
                    _heartbeatLoading = true;
                    Catalog.LoadAssetAsync<AudioClip>(Catalog.gameData.heartBeatLoopAddress, clip =>
                    {
                        _heartbeatLoading = false;
                        if (_heartbeatSource != null)
                        {
                            _heartbeatSource.clip = clip;
                        }
                        Dmcs.Log.Debug(Channel, "signal: heartbeat clip " + (clip == null ? "missing" : "loaded"));
                    }, "DMCS");
                }
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
            var spec = CurrentSpec();
            if (_frames == null || !Same(spec, _framesSpec))
            {
                BuildFrames(spec);
            }
            if (_vignette == null || _vignette.transform.parent != cam.transform)
            {
                if (_vignette != null)
                {
                    UnityEngine.Object.Destroy(_vignette);
                }
                _vignette = NewCap(cam);
            }
            return true;
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

        private static void BuildFrames(VignetteSpec spec)
        {
            var started = Time.realtimeSinceStartup;
            if (_frames != null)
            {
                foreach (var t in _frames)
                {
                    UnityEngine.Object.Destroy(t);
                }
            }
            var pixels = VignetteFlipbook.Generate(spec, 1);
            _frames = new Texture2D[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                var t = new Texture2D(spec.Size, spec.Size, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "DMCS Vignette " + i,
                };
                t.LoadRawTextureData(pixels[i]);
                t.Apply(false, true);
                _frames[i] = t;
            }
            _framesSpec = spec;
            _shownFrame = -1;
            Dmcs.Log.Debug(Channel, "signal: vignette " + pixels.Length + " frames built in "
                + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms");
        }

        private static bool Same(VignetteSpec a, VignetteSpec b) =>
            a.Size == b.Size && a.Frames == b.Frames && a.Streaks == b.Streaks
            && Mathf.Approximately(a.Length, b.Length) && Mathf.Approximately(a.HueDegrees, b.HueDegrees);

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
            var mesh = new Mesh { name = "DMCS Vignette Cap", vertices = vertices, uv = uvs, triangles = triangles };
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
