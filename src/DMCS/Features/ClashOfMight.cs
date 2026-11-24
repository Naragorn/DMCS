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
    /// Clash of Might (blade lock). An NPC's big swing (long wind-up, read from the running AttackMotion at
    /// AttackStage.WindUp) flashes at the middle of its blade; when the strike starts and the NPC is in reach, the
    /// player's blade near the flash and roughly at a right angle to the swing, time slows down until the swing ends.
    /// A block at the flash point: in the Simple mode the enemy's sword flies away; in the On mode the blades lock and
    /// the player pushes against the enemy over a few rounds (ClashRounds), then wins (disarm, stagger or kneel,
    /// Adrenaline) or loses (disarm and/or hit).
    /// </summary>
    public static class ClashOfMight
    {
        private const string Channel = "Clash";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const float GlintSize = 0.3f;
        private const int GlintTextureSize = 128;

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ClashOfMight, ClashModes.IsActive(ClashModes.Default), Dmcs.Log);

        private static string _mode = ClashModes.Default;
        private static readonly System.Random Dice = new System.Random();
        private static ClashRounds _rounds;
        private static float _lastTick;
        private static float _nextHaptic;
        private static float _enemyPeak;
        private static ConfigurableJoint _hold;
        private static Vector3 _lockOnEnemyBlade;
        private static readonly List<(float At, float Strength, RagdollHand Hand)> CueTicks = new List<(float At, float Strength, RagdollHand Hand)>();
        private static readonly Dictionary<WavData, AudioClip> DarkMessiahClips = new Dictionary<WavData, AudioClip>();

        private static readonly ClashTelegraph<int> Telegraph = new ClashTelegraph<int>(ClashSettings.JudgeWindow);
        private static readonly Dictionary<int, Flash> Flashes = new Dictionary<int, Flash>();
        private static readonly ClashLock Lock = new ClashLock();

        /// <summary>Whether the blades are locked now, and where (for diagnostics and probes).</summary>
        internal static bool IsLocked => Lock.IsLocked;

        /// <summary>The blades are locked with this NPC right now.</summary>
        internal static bool IsLockedWith(Creature npc) => npc != null && Lock.IsLocked && _lockNpc == npc.GetInstanceID();

        internal static Vector3? LockPoint => Lock.IsLocked && _lockFlash != null && _lockFlash.Weapon != null ? LockPointNow() : (Vector3?)null;
        private static int _lockNpc;
        private static Flash _lockFlash;
        private static Item _lockItem;
        private static RagdollHand _lockHand;
        private static float _lockAnimatorSpeed = 1f;
        private static float _lockMass;
        private static bool _hooked;
        private static Material _material;
        private static Texture2D _texture;
        private static bool _shaderMissingLogged;
        private static bool _timeOwned;
        private static float _timeScale;
        private static float _timeEndsAt;
        private const int SoundRate = 48000;
        private static readonly Dictionary<string, System.Threading.Tasks.Task<float[]>> SoundTasks = new Dictionary<string, System.Threading.Tasks.Task<float[]>>();
        private static readonly Dictionary<string, AudioClip> SoundClips = new Dictionary<string, AudioClip>();
        private static GameObject _audioHost;
        private static AudioSource _audio;
        private static AudioSource _darkMessiahAudio;

        [ModOption(
            name = ClashModes.ModeName,
            tooltip = "Off. Simple: block exactly where an enemy's blade flashes and its sword flies away. On: the blades lock and you push against the enemy over a few rounds.",
            valueSourceName = nameof(ModeValues),
            category = DmcsCategories.ClashOfMight,
            categoryOrder = DmcsCategories.ClashOfMightOrder,
            order = 0)]
        public static string Mode
        {
            get => _mode;
            set
            {
                var mode = ClashModes.Normalize(value);
                if (mode == _mode)
                {
                    return;
                }
                _mode = mode;
                Dmcs.Log.Debug(Channel, "mode " + mode);
                if (!ClashModes.Locks(mode))
                {
                    EndLock(Lock.Break(), "mode " + mode);
                }
                Toggle.Set(ClashModes.IsActive(mode));
            }
        }

        public static ModOptionString[] ModeValues() => ClashModes.All.Select(v => new ModOptionString(v, v)).ToArray();

        [ModOption(name = ClashFightSettings.DifficultyName, tooltip = "How hard enemies push back in a blade lock. 0: barely; 50: some; 100: twice as hard; 200: you hardly have a chance.", valueSourceName = nameof(DifficultyValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 23)]
        [ModOptionSlider]
        public static float Difficulty { get; set; } = ClashFightSettings.Difficulty.Default;

        [ModOption(name = ClashFightSettings.ScalesWithHeightName, tooltip = "Taller enemies than you push harder, smaller ones weaker (in proportion to their height), and smaller ones last fewer rounds.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 24)]
        public static bool ScalesWithHeight { get; set; } = ClashFightSettings.DefaultScalesWithHeight;

        [ModOption(name = ClashFightSettings.ScalesWithGenderName, tooltip = "Female enemies push weaker (by the reduction below) and last fewer rounds.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 25)]
        public static bool ScalesWithGender { get; set; } = ClashFightSettings.DefaultScalesWithGender;

        [ModOption(name = ClashFightSettings.FemaleStrengthName, tooltip = "How much weaker (percent) a female enemy pushes. Women have about 40-60 % of men's upper-body strength.", valueSourceName = nameof(FemaleStrengthValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 26)]
        [ModOptionSlider]
        public static float FemaleStrength { get; set; } = ClashFightSettings.FemaleStrength.Default;

        [ModOption(name = ClashFightSettings.MinRoundsName, tooltip = "Fewest rounds of a blade lock (each lock picks a random number between min and max).", valueSourceName = nameof(RoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 27)]
        [ModOptionSlider]
        public static float MinRounds { get; set; } = ClashFightSettings.MinRounds.Default;

        [ModOption(name = ClashFightSettings.MaxRoundsName, tooltip = "Most rounds of a blade lock.", valueSourceName = nameof(RoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 28)]
        [ModOptionSlider]
        public static float MaxRounds { get; set; } = ClashFightSettings.MaxRounds.Default;

        [ModOption(name = ClashFightSettings.FewerRoundsName, tooltip = "Rounds fewer against a smaller or a female enemy (while the matching scaling is on); at least one round stays.", valueSourceName = nameof(FewerRoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 29)]
        [ModOptionSlider]
        public static float FewerRounds { get; set; } = ClashFightSettings.FewerRounds.Default;

        [ModOption(name = ClashFightSettings.EnemyPushName, tooltip = "How far (m) your real hand must lead your heavy weapon towards the enemy to match its push at its strongest, at difficulty 50.", valueSourceName = nameof(EnemyPushValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 30)]
        [ModOptionSlider]
        public static float EnemyPush { get; set; } = ClashFightSettings.EnemyPush.Default;

        [ModOption(name = ClashFightSettings.DeadZoneName, tooltip = "How far (m) your hands may move since the lock without it counting as a push: hands are never quite still.", valueSourceName = nameof(DeadZoneValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 31)]
        [ModOptionSlider]
        public static float DeadZone { get; set; } = ClashFightSettings.DeadZone.Default;

        [ModOption(name = ClashFightSettings.PushPhaseName, tooltip = "Seconds of one push round; the enemy's push swells and ebbs once in it.", valueSourceName = nameof(PushPhaseValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 32)]
        [ModOptionSlider]
        public static float PushPhase { get; set; } = ClashFightSettings.PushPhase.Default;

        [ModOption(name = ClashFightSettings.SidePushName, tooltip = "Pushing the crossed blades to your right or left counts too. Win a lock pushing mostly sideways and the enemy's sword always flies to that side; the star turns gold while you push sideways.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 33)]
        public static bool SidePush { get; set; } = ClashFightSettings.DefaultSidePush;

        [ModOption(name = ClashFightSettings.WinDisarmsName, tooltip = "Winning a blade lock pushing forward knocks the enemy's sword away too. Off: it staggers or kneels (a sideways win always disarms).", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 35)]
        public static bool WinDisarms { get; set; } = ClashFightSettings.DefaultWinDisarms;

        [ModOption(name = ClashFightSettings.WinKneelChanceName, tooltip = "Chance (percent) that a beaten enemy goes down on its knees and you can execute it. Otherwise it staggers.", valueSourceName = nameof(WinKneelChanceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 36)]
        [ModOptionSlider]
        public static float WinKneelChance { get; set; } = ClashFightSettings.WinKneelChance.Default;

        [ModOption(name = ClashFightSettings.LoseOutcomeName, tooltip = "What a lost blade lock costs you: your weapon flies away, you take a hit, both, or nothing.", valueSourceName = nameof(LoseOutcomeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 37)]
        public static string LoseOutcome { get; set; } = ClashFightSettings.DefaultLoseOutcome;

        [ModOption(name = ClashFightSettings.LoseDamageName, tooltip = "Health you lose with the hit of a lost blade lock (you have 100).", valueSourceName = nameof(LoseDamageValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 38)]
        [ModOptionSlider]
        public static float LoseDamage { get; set; } = ClashFightSettings.LoseDamage.Default;

        [ModOption(name = ClashFightSettings.HapticsName, tooltip = "Strength of the vibration in your weapon hand during a blade lock (it follows the enemy's push).", valueSourceName = nameof(HapticsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 39)]
        [ModOptionSlider]
        public static float Haptics { get; set; } = ClashFightSettings.Haptics.Default;

        [ModOption(name = ClashFightSettings.DuelStarName, tooltip = "The star at the crossed blades shows who is ahead in the round: green and bigger when you are, red when the enemy is.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 40)]
        public static bool DuelStar { get; set; } = ClashFightSettings.DefaultDuelStar;

        [ModOption(name = ClashFightSettings.OutcomeCuesName, tooltip = "A won blade lock throws sparks with a victory sound and three strong beats in your hand; a lost one gives a dull thud and a long rumble.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 41)]
        public static bool OutcomeCues { get; set; } = ClashFightSettings.DefaultOutcomeCues;

        [ModOption(name = ClashFightSettings.DarkMessiahSoundsName, tooltip = "Dark Messiah's own blade lock and disarm sounds, read from your Dark Messiah install (never shipped). Falls back to Blade & Sorcery's sounds when it is not found.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 42)]
        public static bool DarkMessiahSounds
        {
            get => _darkMessiahSounds;
            set
            {
                _darkMessiahSounds = value;
                if (value)
                {
                    DarkMessiahAssets.Begin();
                }
            }
        }

        private static bool _darkMessiahSounds = ClashFightSettings.DefaultDarkMessiahSounds;

        public static ModOptionFloat[] DifficultyValues() => Floats(ClashFightSettings.Difficulty, "0", "");

        public static ModOptionFloat[] FemaleStrengthValues() => Floats(ClashFightSettings.FemaleStrength, "0", " %");

        public static ModOptionFloat[] RoundsValues() => Floats(ClashFightSettings.MinRounds, "0", "");

        public static ModOptionFloat[] FewerRoundsValues() => Floats(ClashFightSettings.FewerRounds, "0", "");

        public static ModOptionFloat[] EnemyPushValues() => Floats(ClashFightSettings.EnemyPush, "0.00", " m");

        public static ModOptionFloat[] DeadZoneValues() => Floats(ClashFightSettings.DeadZone, "0.00", " m");

        public static ModOptionFloat[] PushPhaseValues() => Floats(ClashFightSettings.PushPhase, "0.00", " s");

        public static ModOptionFloat[] WinKneelChanceValues() => Floats(ClashFightSettings.WinKneelChance, "0", " %");

        public static ModOptionString[] LoseOutcomeValues() => ClashLoseOutcomes.All.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionFloat[] LoseDamageValues() => Floats(ClashFightSettings.LoseDamage, "0", "");

        public static ModOptionFloat[] HapticsValues() => Floats(ClashFightSettings.Haptics, "0.0", "");

        [ModOption(name = ClashSettings.OnlySwordsName, tooltip = "Only enemies with a sword flash. Off: any blade that cuts.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 1)]
        public static bool OnlySwords { get; set; } = ClashSettings.DefaultOnlySwords;

        [ModOption(name = ClashSettings.MinWindUpName, tooltip = "How long (seconds) an enemy must wind up for its swing to flash. Plain swings take 0.13–0.37 s, big ones 0.40–0.77 s.", valueSourceName = nameof(MinWindUpValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 2)]
        [ModOptionSlider]
        public static float MinWindUp { get; set; } = ClashSettings.MinWindUp.Default;

        [ModOption(name = ClashSettings.SlowMoName, tooltip = "Time slows when a flashing swing comes and your blade is ready to block it.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 3)]
        public static bool SlowMo { get; set; } = ClashSettings.DefaultSlowMo;

        [ModOption(name = ClashSettings.SlowMoScaleName, tooltip = "World speed during the clash slow-motion.", valueSourceName = nameof(SlowMoScaleValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 4)]
        [ModOptionSlider]
        public static float SlowMoScale { get; set; } = ClashSettings.SlowMoScale.Default;

        [ModOption(name = ClashSettings.NpcReachName, tooltip = "How close (m) the enemy must be for the slow-motion.", valueSourceName = nameof(NpcReachValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 5)]
        [ModOptionSlider]
        public static float NpcReach { get; set; } = ClashSettings.NpcReach.Default;

        [ModOption(name = ClashSettings.FlashAtTopName, tooltip = "The blade flashes when the enemy has raised it all the way, just before the strike, like sunlight caught on it. Off: as soon as the wind-up starts (more warning).", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 22)]
        public static bool FlashAtTop { get; set; } = ClashSettings.DefaultFlashAtTop;

        [ModOption(name = ClashSettings.WeaponWeightName, tooltip = "How much heavier your weapon gets while the blades are locked, so you cannot just push through.", valueSourceName = nameof(WeaponWeightValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 10)]
        [ModOptionSlider]
        public static float WeaponWeight { get; set; } = ClashSettings.WeaponWeight.Default;

        [ModOption(name = ClashSettings.HoldBladeName, tooltip = "Your blade stays where the blades met for the whole lock, however hard you swung into it. Off: only its weight holds it back.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 11)]
        public static bool HoldBlade { get; set; } = ClashSettings.DefaultHoldBlade;

        [ModOption(name = ClashSettings.HoldStyleName, tooltip = "Spring: your held blade gives a little and springs back to where the blades met. Hard: it stops dead.", valueSourceName = nameof(HoldStyleValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 12)]
        public static string HoldStyle { get; set; } = ClashSettings.DefaultHoldStyle;

        [ModOption(name = ClashSettings.HoldSlackName, tooltip = "How far (m) your held blade moves freely around the point where the blades met before the hold catches it. 0: not at all.", valueSourceName = nameof(HoldSlackValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 13)]
        [ModOptionSlider]
        public static float HoldSlack { get; set; } = ClashSettings.HoldSlack.Default;

        [ModOption(name = ClashSettings.HoldSpringName, tooltip = "Spring style: how firmly the spring pulls your blade back. Higher is firmer.", valueSourceName = nameof(HoldSpringValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 14)]
        [ModOptionSlider]
        public static float HoldSpring { get; set; } = ClashSettings.HoldSpring.Default;

        [ModOption(name = ClashSettings.HoldSpringDampingName, tooltip = "Spring style: how quickly the spring settles. 0: it bounces a while. 1: no bounce.", valueSourceName = nameof(HoldSpringDampingValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 15)]
        [ModOptionSlider]
        public static float HoldSpringDamping { get; set; } = ClashSettings.HoldSpringDamping.Default;

        [ModOption(name = ClashSettings.HoldRotationName, tooltip = "Your held blade also cannot turn around the point where the blades met. Off: it can.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 16)]
        public static bool HoldRotation { get; set; } = ClashSettings.DefaultHoldRotation;

        [ModOption(name = ClashSettings.HoldBrieflyName, tooltip = "Hold your blade only for the first moment of the lock, to catch your swing. After that only its weight holds it back.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 17)]
        public static bool HoldBriefly { get; set; } = ClashSettings.DefaultHoldBriefly;

        [ModOption(name = ClashSettings.HoldTimeName, tooltip = "How long (s) the hold lasts with Clash Hold Briefly.", valueSourceName = nameof(HoldTimeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 18)]
        [ModOptionSlider]
        public static float HoldTime { get; set; } = ClashSettings.HoldTime.Default;


        public static ModOptionFloat[] WeaponWeightValues() => Floats(ClashSettings.WeaponWeight, "0", "x");

        public static ModOptionString[] HoldStyleValues() => ClashHold.Styles.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionFloat[] HoldSlackValues() => Floats(ClashSettings.HoldSlack, "0.00", " m");

        public static ModOptionFloat[] HoldSpringValues() => Floats(ClashSettings.HoldSpring, "0.0", "");

        public static ModOptionFloat[] HoldSpringDampingValues() => Floats(ClashSettings.HoldSpringDamping, "0.0", "");

        public static ModOptionFloat[] HoldTimeValues() => Floats(ClashSettings.HoldTime, "0.00", " s");

        [ModOption(name = ClashSettings.FlashSoundName, tooltip = "Sound when an enemy's blade flashes. DMCS 1 is modelled on Sekiro's perilous-attack cue; DMCS 2 is a short one, DMCS 3 a darker one, DMCS 4 a bright one. Combines with the game sound below.", valueSourceName = nameof(FlashSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 19)]
        public static string FlashSoundStyle { get; set; } = ClashSettings.DefaultFlashSound;

        [ModOption(name = ClashSettings.FlashGameSoundName, tooltip = "One of Blade & Sorcery's own sounds with the flash (Metal Hit also sparks). Plays together with the sound above.", valueSourceName = nameof(FlashGameSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 20)]
        public static string FlashGameSound { get; set; } = ClashSettings.DefaultFlashGameSound;

        [ModOption(name = ClashSettings.FlashSoundVolumeName, tooltip = "Volume of the DMCS flash sound.", valueSourceName = nameof(FlashSoundVolumeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 21)]
        [ModOptionSlider]
        public static float FlashSoundVolume { get; set; } = ClashSettings.FlashSoundVolume.Default;

        public static ModOptionString[] FlashSoundValues() => FlashSound.Styles.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionString[] FlashGameSoundValues() => ClashSettings.GameSounds.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionFloat[] FlashSoundVolumeValues() => Floats(ClashSettings.FlashSoundVolume, "0.00", "");

        public static ModOptionFloat[] MinWindUpValues() => Floats(ClashSettings.MinWindUp, "0.00", " s");

        public static ModOptionFloat[] SlowMoScaleValues() => Floats(ClashSettings.SlowMoScale, "0.00", "x");

        public static ModOptionFloat[] NpcReachValues() => Floats(ClashSettings.NpcReach, "0.00", " m");

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (_hooked)
            {
                return;
            }
            EventManager.onCreatureAttacking += OnCreatureAttacking;
            EventManager.onCreatureAttackParry += OnParry;
            _hooked = true;
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureAttacking -= OnCreatureAttacking;
                EventManager.onCreatureAttackParry -= OnParry;
                _hooked = false;
            }
            ResetAll();
        }

        /// <summary>Level unloading or feature off: flashes gone, our slow-motion ended.</summary>
        public static void ResetAll()
        {
            EndLock(Lock.Break(), "reset");
            foreach (var id in Telegraph.Clear())
            {
                RemoveFlash(id);
            }
            foreach (var id in Flashes.Keys.ToList())
            {
                RemoveFlash(id);
            }
            CueTicks.Clear();
            EndTime(force: true);
        }

        public static void Update()
        {
            UpdateLock();
            PlayCueTicks();
            if (_timeOwned && Time.unscaledTime >= _timeEndsAt)
            {
                Dmcs.Log.Debug(Channel, "slow-motion cap reached");
                EndTime(force: false);
            }
            if (Flashes.Count == 0)
            {
                return;
            }
            var cam = Player.local != null && Player.local.head != null ? Player.local.head.cam : null;
            foreach (var pair in Flashes.ToList())
            {
                var flash = pair.Value;
                if (flash.Weapon == null || flash.Glint == null || flash.Weapon.mainHandler == null)
                {
                    Telegraph.Ended(pair.Key);
                    RemoveFlash(pair.Key);
                    continue;
                }
                if (!flash.ShownAt.HasValue)
                {
                    var tip = flash.TipHeight();
                    if (flash.Top == null || (tip.HasValue && flash.Top.Sample(Time.time - flash.WindUpAt, tip.Value)))
                    {
                        Show(flash);
                    }
                }
                var age = flash.ShownAt.HasValue ? Time.unscaledTime - flash.ShownAt.Value : 0f;
                // In a lock the star moves to the crossed blades and shows who is ahead in the round (Clash Duel Star).
                var duel = DuelStar && Lock.IsLocked && pair.Key == _lockNpc && _rounds != null;
                var advantage = duel ? _rounds.Advantage : 0f;
                flash.Glint.transform.position = duel ? LockPointNow() : flash.Point;
                flash.Glint.transform.localScale = Vector3.one * (GlintSize * Glint.Scale(age) * (duel ? ClashDuelSignal.Size(advantage) : 1f));
                if (cam != null)
                {
                    flash.Glint.transform.rotation = Quaternion.LookRotation(flash.Glint.transform.position - cam.transform.position)
                        * Quaternion.Euler(0f, 0f, Glint.SpinDegrees(age));
                }
                var (r, g, b) = ClashDuelSignal.Tint(advantage, duel && _rounds.PushingSideways);
                flash.Renderer.material.SetColor("_BaseColor", new Color(r, g, b, duel ? 1f : Glint.Pulse(age)));
            }
        }

        /// <summary>Each physics step: judge the slow-motion of every swing that just started its strike.</summary>
        public static void FixedUpdate()
        {
            if (!Toggle.Enabled || Flashes.Count == 0)
            {
                return;
            }
            var now = Time.unscaledTime;
            foreach (var pair in Flashes.ToList())
            {
                var id = pair.Key;
                if (Telegraph.IsJudging(id, now))
                {
                    var verdict = Judge(pair.Value);
                    if (Telegraph.Verdict(id, verdict, now))
                    {
                        StartTime(pair.Value);
                    }
                }
                var over = Telegraph.JudgingOver(id, now);
                if (over.HasValue)
                {
                    Dmcs.Log.Debug(Channel, "no slow-motion for " + pair.Value.NpcName + "'s swing: " + over.Value + " (" + pair.Value.LastDetail + ")");
                }
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ResetAll();
            }
        }

        private static void OnCreatureAttacking(Creature attacker, Creature target, Transform targetTransform,
            BrainModuleAttack.AttackType type, BrainModuleAttack.AttackStage stage)
        {
            try
            {
                if (!Toggle.Enabled || attacker == null || attacker.isPlayer || type != BrainModuleAttack.AttackType.Melee)
                {
                    return;
                }
                var id = attacker.GetInstanceID();
                switch (stage)
                {
                    case BrainModuleAttack.AttackStage.WindUp:
                        WindUp(attacker, target, id);
                        break;
                    case BrainModuleAttack.AttackStage.Attack:
                        Telegraph.StrikeStarted(id, Time.unscaledTime);
                        if (Flashes.TryGetValue(id, out var striking) && !striking.ShownAt.HasValue)
                        {
                            // No top seen before the strike: the flash shows now at the latest.
                            Show(striking);
                        }
                        break;
                    case BrainModuleAttack.AttackStage.End:
                    case BrainModuleAttack.AttackStage.Cancel:
                        if (Lock.IsLocked && _lockNpc == id)
                        {
                            break;
                        }
                        if (Telegraph.Ended(id))
                        {
                            EndTime(force: false);
                        }
                        RemoveFlash(id);
                        break;
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash error: " + ex);
            }
        }

        private static void WindUp(Creature attacker, Creature target, int id)
        {
            if (Telegraph.Ended(id))
            {
                EndTime(force: false);
            }
            RemoveFlash(id);
            if (target == null || !target.isPlayer)
            {
                return;
            }
            var melee = attacker.brain.instance?.GetModule<BrainModuleMelee>(false);
            var attack = melee?.attack;
            if (attack == null)
            {
                return;
            }
            var side = attack.attackSide == Interactable.HandSide.Left ? Side.Left : Side.Right;
            var windUp = attack.GetRealStartTime(melee.GetBrainSpeedMultiplier(side));
            var thrust = attack.attackMotion == ItemModuleAI.AttackType.Thrust;
            var weapon = attacker.equipment?.GetHeldItem(side);
            var name = attacker.name;
            if (!ClashRules.IsBigSwing(thrust, windUp, MinWindUp))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " (wind-up " + F(windUp) + " s): not a big swing");
                return;
            }
            if (weapon == null)
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + ": no weapon in the " + side + " hand");
                return;
            }
            if (!TryBladeMiddle(weapon, attacker.GetHand(side), out var local))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " with " + weapon.data?.id + ": no slashing blade ("
                    + string.Join(", ", weapon.collisionHandlers.SelectMany(h => h.damagers).Where(d => d != null)
                        .Select(d => (d.data?.id ?? "?") + "/" + (d.data?.damageModifierData?.damageType.ToString() ?? "?"))) + ")");
                return;
            }
            var isSword = ClashRules.IsSword(weapon.data?.category, weapon.data?.id);
            if (!ClashRules.CanFlash(OnlySwords, isSword, true))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " with " + weapon.data?.id + ": not a sword");
                return;
            }
            var glint = NewGlint();
            if (glint == null)
            {
                return;
            }
            Telegraph.WindUp(id);
            var flash = new Flash(attacker, weapon, attacker.GetHand(side), local, glint, Time.unscaledTime)
            {
                WindUpAt = Time.time,
                WindUpSeconds = windUp,
                Top = FlashAtTop ? new SwingTop(windUp) : null,
            };
            Flashes[id] = flash;
            Dmcs.Log.Debug(Channel, name + " winds up " + attack.id + " (" + F(windUp) + " s) with " + weapon.data?.id + ": flash" + (FlashAtTop ? " at the swing top" : string.Empty));
            if (!FlashAtTop)
            {
                Show(flash);
            }
        }

        /// <summary>The flash appears (at the swing top, or at once): its burst starts, its sound plays.</summary>
        private static void Show(Flash flash)
        {
            flash.ShownAt = Time.unscaledTime;
            var into = Time.time - flash.WindUpAt;
            Dmcs.Log.Debug(Channel, flash.NpcName + "'s blade flashes " + (flash.Top != null ? "at the swing top" : "as the wind-up starts") + " ("
                + F(into) + " of " + F(flash.WindUpSeconds) + " s)" + PlayFlashSound(flash.Point));
        }

        /// <summary>The middle of the weapon's slashing blade along handle → tip, as a point local to the weapon.</summary>
        private static bool TryBladeMiddle(Item weapon, RagdollHand hand, out Vector3 local)
        {
            local = Vector3.zero;
            var handle = hand != null && hand.grabbedHandle != null ? hand.grabbedHandle.transform.position : weapon.transform.position;
            var colliders = BladeColliders(weapon);
            if (colliders.Count == 0)
            {
                return false;
            }
            weapon.GetFurthestDamagerCollider(out _, out var tipCollider);
            var tip = tipCollider != null ? tipCollider.bounds.center : colliders[0].bounds.center;
            var axis = (tip - handle).normalized;
            if (axis == Vector3.zero)
            {
                return false;
            }
            var projections = colliders.SelectMany(Corners).Select(p => Vector3.Dot(p - handle, axis));
            var middle = ClashGeometry.MiddleOfExtent(projections);
            if (!middle.HasValue)
            {
                return false;
            }
            local = weapon.transform.InverseTransformPoint(handle + (axis * middle.Value));
            return true;
        }

        /// <summary>The weapon's blade: the colliders of its slash damagers (a damager's own collider, else its group's).</summary>
        private static List<Collider> BladeColliders(Item weapon)
        {
            var colliders = new List<Collider>();
            foreach (var handler in weapon.collisionHandlers)
            {
                foreach (var damager in handler.damagers)
                {
                    if (damager == null || damager.data?.damageModifierData == null || damager.data.damageModifierData.damageType != DamageType.Slash)
                    {
                        continue;
                    }
                    if (damager.colliderOnly != null)
                    {
                        colliders.Add(damager.colliderOnly);
                    }
                    else if (damager.colliderGroup != null)
                    {
                        colliders.AddRange(damager.colliderGroup.colliders.Where(c => c != null));
                    }
                }
            }
            return colliders;
        }

        /// <summary>
        /// Whether the block was blade on blade: the game's collision names the collider (and its group) of each weapon that
        /// touched; each must be one of that weapon's blade colliders (BladeColliders). Without a collider the group counts
        /// when all its colliders are blade. `where` names the touching parts for the log.
        /// </summary>
        private static bool BladesMeet(CollisionInstance hit, Item theirs, Item mine, out string where)
        {
            var theirPart = Touching(hit, theirs, out var theirName);
            var myPart = Touching(hit, mine, out var myName);
            where = "your " + myName + " on its " + theirName;
            return theirPart && myPart;
        }

        private static bool Touching(CollisionInstance hit, Item weapon, out string name)
        {
            var blade = BladeColliders(weapon);
            foreach (var collider in new[] { hit.sourceCollider, hit.targetCollider })
            {
                if (collider != null && weapon.colliderGroups.Any(g => g != null && g.colliders.Contains(collider)))
                {
                    var onBlade = blade.Contains(collider);
                    name = collider.name + (onBlade ? " (blade)" : " (not blade)");
                    return onBlade;
                }
            }
            foreach (var group in new[] { hit.sourceColliderGroup, hit.targetColliderGroup })
            {
                if (group != null && weapon.colliderGroups.Contains(group))
                {
                    var onBlade = group.colliders.Count > 0 && group.colliders.All(c => c == null || blade.Contains(c));
                    name = "group " + group.name + (onBlade ? " (blade)" : " (not blade)");
                    return onBlade;
                }
            }
            name = "unknown part";
            return false;
        }

        private static IEnumerable<Vector3> Corners(Collider c)
        {
            var b = c.bounds;
            for (var i = 0; i < 8; i++)
            {
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            }
        }

        private static SlowMoVerdict Judge(Flash flash)
        {
            var player = Player.currentCreature;
            if (player == null)
            {
                return SlowMoVerdict.NpcTooFar;
            }
            var npcDistance = Vector3.Distance(flash.Npc.transform.position, player.transform.position);
            var twoHanded = TwoHandedWeapon(player);
            flash.LastDetail = "enemy " + F(npcDistance) + " m, " + (twoHanded != null ? "both hands on " + twoHanded.data?.id : "no weapon in both hands");
            return ClashRules.JudgeSlowMo(npcDistance, NpcReach, twoHanded != null);
        }

        /// <summary>The item the player holds with both hands (the game's own Item.IsTwoHanded), or null.</summary>
        private static Item TwoHandedWeapon(Creature player)
        {
            foreach (var hand in new[] { player.handRight, player.handLeft })
            {
                var item = hand != null && hand.grabbedHandle != null ? hand.grabbedHandle.item : null;
                if (item != null && item.IsTwoHanded())
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>The player's held blade as handle → tip; false when the hand holds nothing with a damager.</summary>
        private static bool TryPlayerBlade(RagdollHand hand, out Vector3 handle, out Vector3 tip)
        {
            handle = tip = Vector3.zero;
            var item = hand != null && hand.grabbedHandle != null ? hand.grabbedHandle.item : null;
            if (item == null)
            {
                return false;
            }
            item.GetFurthestDamagerCollider(out _, out var tipCollider);
            if (tipCollider == null)
            {
                return false;
            }
            handle = hand.grabbedHandle.transform.position;
            tip = tipCollider.bounds.center;
            return (tip - handle).sqrMagnitude > 0.0001f;
        }

        private static void StartTime(Flash flash)
        {
            if (!SlowMo)
            {
                Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing (" + flash.LastDetail + "); slow-motion is off");
                return;
            }
            if (_timeOwned || TimeManager.slowMotionState != TimeManager.SlowMotionState.Disabled)
            {
                Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing; no slow-motion: another one runs (" + TimeManager.slowMotionState + ")");
                return;
            }
            _timeScale = SlowMoScale;
            TimeManager.SetSlowMotion(true, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.1f, 1f), Catalog.GetData<EffectData>("SpellSlowTime", false));
            _timeOwned = true;
            _timeEndsAt = Time.unscaledTime + ClashSettings.SlowMoMaxSeconds;
            Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing (" + flash.LastDetail + "): slow-motion " + F(_timeScale) + "x");
        }

        private static void EndTime(bool force)
        {
            if (!_timeOwned)
            {
                return;
            }
            _timeOwned = false;
            var playerDead = Player.currentCreature == null || Player.currentCreature.isKilled;
            // Only end our own slow-motion: the player's Focus, a critical or the game's death slow-motion may have taken over.
            var starting = TimeManager.slowMotionState == TimeManager.SlowMotionState.Starting;
            if (playerDead || (!force && !ClashRules.StillOurSlowMotion(starting, Time.timeScale, _timeScale)))
            {
                Dmcs.Log.Debug(Channel, "slow-motion over; another slow-motion took over, left running");
                return;
            }
            TimeManager.SetSlowMotion(false, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.1f, 1f));
            Dmcs.Log.Debug(Channel, "slow-motion over: time back to normal");
        }

        /// <summary>
        /// The NPC's swing hit the player's weapon (the game's own block event, raised by BrainModuleMelee when an attack
        /// hits a held weapon at more than ~1.4 m/s). At the flash point and across the swing the blades lock; anything else
        /// stays an ordinary parry.
        /// </summary>
        private static void OnParry(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            try
            {
                if (!Toggle.Enabled || Lock.IsLocked || parried == null || parried.isPlayer || parrying == null || !parrying.isPlayer || parryingItem == null || hit == null)
                {
                    return;
                }
                var id = parried.GetInstanceID();
                if (!Flashes.TryGetValue(id, out var flash))
                {
                    return;
                }
                if (parriedItem != null && parriedItem != flash.Weapon)
                {
                    Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s other weapon (" + parriedItem.data?.id + "), not the flashing one: an ordinary parry");
                    return;
                }
                var point = flash.Point;
                var contactToFlash = Vector3.Distance(hit.contactPoint, point);
                var hand = parryingItem.mainHandler;
                var twoHanded = parryingItem.IsTwoHanded();
                var handSpeed = hand != null ? HandSpeeds.Peak(hand.side, Parry.SpeedWindow) : 0f;
                var bladesMeet = BladesMeet(hit, flash.Weapon, parryingItem, out var where);
                var verdict = ClashRules.JudgeBlock(twoHanded, bladesMeet);
                // Contact and hand speed only for the log (the headset values): hands and blades decide (user 2026-11-24).
                var detail = (twoHanded ? "both hands" : "one hand") + " on " + parryingItem.data?.id + ", " + where + ", contact " + F(contactToFlash)
                    + " m from the flash, hand " + F(handSpeed) + " m/s";
                if (verdict != BlockVerdict.Clash)
                {
                    Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s swing: " + verdict + " (" + detail + "), an ordinary parry; a clash needs both hands on the weapon and blade on blade");
                    return;
                }
                if (!ClashModes.Locks(Mode))
                {
                    SimpleDisarm(id, flash, hit, detail);
                    return;
                }
                StartLock(id, flash, parryingItem, hand, detail, hit.contactPoint);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash error: " + ex);
            }
        }

        /// <summary>
        /// The simple variant (user 2026-11-18): a block exactly at the star knocks the enemy's sword away, through Disarm
        /// Strike's path (fling away from the player, no-regrab lock, catch bonus); Clash Win adrenaline as for a won lock.
        /// </summary>
        private static void SimpleDisarm(int id, Flash flash, CollisionInstance hit, string detail)
        {
            EndTime(force: false);
            var npc = flash.Npc;
            var away = AwayFromPlayer(npc);
            Spark(hit.contactPoint);
            var disarmed = DisarmStrike.DisarmAlong(npc, flash.Hand, away, "clash at the star", hit);
            PlayDarkMessiahDisarm(disarmed);
            Telegraph.Ended(id);
            RemoveFlash(id);
            if (!disarmed)
            {
                Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s swing at the star (" + detail + "), but it holds no weapon there any more");
                return;
            }
            Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s swing at the star (" + detail + "): its sword flies away (simple clash)");
            Adrenaline.Add(AdrenalineSource.ClashWin, flash.NpcName);
        }

        private static void StartLock(int id, Flash flash, Item item, RagdollHand hand, string detail, Vector3 contact)
        {
            EndTime(force: false);
            _lockNpc = id;
            _lockFlash = flash;
            _lockItem = item;
            _lockHand = hand;
            RememberControllers(item);
            _lockOnEnemyBlade = flash.Weapon.transform.InverseTransformPoint(contact);
            _lockAnimatorSpeed = flash.Npc.animator != null ? flash.Npc.animator.speed : 1f;
            if (flash.Npc.animator != null)
            {
                flash.Npc.animator.speed = 0f;
            }
            _lockMass = item.physicBody != null ? item.physicBody.mass : 0f;
            if (item.physicBody != null)
            {
                item.physicBody.mass = _lockMass * WeaponWeight;
            }
            // After the weight: the spring is sized on the mass the blade has during the lock.
            var held = Hold(item, flash.Weapon, contact);
            var player = Player.currentCreature;
            var npcHeight = flash.Npc.GetHeight();
            var playerHeight = player != null ? player.GetHeight() : 0f;
            var female = flash.Npc.data != null && flash.Npc.data.gender == CreatureData.Gender.Female;
            var factor = ClashStrength.Factor(Difficulty, ScalesWithHeight, npcHeight, playerHeight, ScalesWithGender, female, FemaleStrength);
            var weaker = ClashStrength.IsWeaker(ScalesWithHeight, npcHeight, playerHeight, ScalesWithGender, female);
            var rounds = ClashStrength.Rounds(Mathf.RoundToInt(MinRounds), Mathf.RoundToInt(MaxRounds), Dice.NextDouble(), weaker, Mathf.RoundToInt(FewerRounds));
            _enemyPeak = EnemyPush * factor;
            _rounds = new ClashRounds(rounds, PushPhase, _enemyPeak, DeadZone);
            _lastTick = Time.unscaledTime;
            _nextHaptic = 0f;
            Lock.Start(Time.unscaledTime);
            Spark(contact);
            PlayDarkMessiahVersus();
            Dmcs.Log.Debug(Channel, "blades locked with " + flash.NpcName + " (" + detail + "): enemy frozen, your " + item.data?.id + " "
                + WeaponWeight.ToString("0", CultureInfo.InvariantCulture) + "x heavier" + held + "; " + rounds + " round" + (rounds == 1 ? "" : "s")
                + " (difficulty " + Difficulty.ToString("0", CultureInfo.InvariantCulture) + ", enemy " + F(npcHeight) + " m vs you " + F(playerHeight) + " m"
                + (female ? ", female" : "") + (weaker ? ", weaker" : "") + "): enemy push " + F(_enemyPeak) + " m at its peak");
        }

        private static void UpdateLock()
        {
            if (!Lock.IsLocked)
            {
                return;
            }
            var npc = _lockFlash?.Npc;
            var gone = npc == null || npc.isKilled ? "the enemy is dead or gone"
                : _lockFlash.Weapon == null || _lockFlash.Weapon.mainHandler == null ? "the enemy let go of its weapon"
                : null;
            if (gone != null)
            {
                EndLock(Lock.Break(), gone);
                return;
            }
            if (_lockItem == null || _lockItem.mainHandler == null || _lockItem.mainHandler.creature == null || !_lockItem.mainHandler.creature.isPlayer)
            {
                // Letting go is a lost clash (user 2026-11-18): the weapon is gone already, so only the hit is left.
                Lose("you let go of your weapon", disarmPlayer: false);
                return;
            }
            if (_lockHand == null || _lockHand.grabbedHandle == null || _lockHand.grabbedHandle.item != _lockItem)
            {
                // One of the two hands let go: the push is read from the hand that still holds the weapon.
                _lockHand = _lockItem.mainHandler;
            }
            var now = Time.unscaledTime;
            if (_hold != null)
            {
                if (!ClashHold.StillHolding(Lock.Elapsed(now), HoldBriefly, HoldTime))
                {
                    ReleaseHold();
                    Dmcs.Log.Debug(Channel, "hold released after " + F(Lock.Elapsed(now)) + " s (Clash Hold Briefly): only the weight holds your blade now");
                }
                else
                {
                    // The frozen enemy blade still settles a little (harness probe 2026-11-17: 4–28 cm in 0.25 s); the hold follows it.
                    _hold.connectedAnchor = LockPointNow();
                }
            }
            var dt = now - _lastTick;
            _lastTick = now;
            Measure(out var lead, out var side, out var gap);
            var step = _rounds.Tick(dt, lead, SidePush ? side : 0f);
            Feel(now);
            if (Lock.DiagnosticDue(now))
            {
                LogDiagnostic(now, lead, side, gap);
            }
            switch (step)
            {
                case ClashStep.PushHeld:
                    Dmcs.Log.Debug(Channel, "round " + (_rounds.Round - 1) + "/" + _rounds.Rounds + " held (you " + F3(_rounds.LastPlayerSum) + " vs the enemy "
                        + F3(_rounds.LastEnemySum) + " m·s): round " + _rounds.Round + " starts");
                    Pulse(1f);
                    Spark(LockPointNow());
                    PlayDarkMessiahVersus();
                    break;
                case ClashStep.Won:
                    Win();
                    break;
                case ClashStep.LostPush:
                    Lose("the enemy pushed harder in round " + _rounds.Round + "/" + _rounds.Rounds + " (you " + F3(_rounds.LastPlayerSum) + " vs the enemy " + F3(_rounds.LastEnemySum) + " m·s)", disarmPlayer: true);
                    break;
            }
        }

        /// <summary>
        /// The push (user 2026-11-24, "Controller-Weg"): how far the player's real controllers (PlayerHand.grip) moved since
        /// the blades locked, the mean over every hand on the weapon (ClashGeometry.ControllerTravel): along the line from
        /// the player's head to the lock point (lead), to the player's right (+) or left (−) square to that line (side;
        /// ClashGeometry.PushComponents) and at all (gap). Read in the player rig's own space, so walking with the stick does
        /// not count while a real step does. The in-game hand is no input any more (before: controller minus hand, which
        /// the weight, the hold and the frozen enemy blade moved by 0.1–0.5 m without the player doing anything).
        /// </summary>
        private static void Measure(out float lead, out float side, out float gap)
        {
            lead = side = gap = 0f;
            if (_lockItem == null || Player.local == null || Player.local.head == null)
            {
                return;
            }
            var rig = Player.local.transform;
            var starts = new List<Vec3>(2);
            var nows = new List<Vec3>(2);
            foreach (var hand in _lockItem.handlers)
            {
                var playerHand = hand != null ? hand.playerHand : null;
                if (playerHand == null || playerHand.grip == null || hand.creature == null || !hand.creature.isPlayer)
                {
                    continue;
                }
                var now = rig.InverseTransformPoint(playerHand.grip.position);
                if (!_lockControllerStart.TryGetValue(hand.side, out var start))
                {
                    // A hand that takes the weapon during the lock starts from where it is.
                    start = now;
                    _lockControllerStart[hand.side] = start;
                }
                starts.Add(V(start));
                nows.Add(V(now));
            }
            if (nows.Count == 0)
            {
                return;
            }
            var localTravel = ClashGeometry.ControllerTravel(starts, nows);
            var travel = V(rig.TransformVector(new Vector3(localTravel.X, localTravel.Y, localTravel.Z)));
            var head = Player.local.head.transform;
            (lead, side) = ClashGeometry.PushComponents(travel, V(head.position), V(LockPointNow()), V(head.right));
            gap = travel.Length;
        }

        /// <summary>Where each player controller on the locked weapon was when the blades locked, in the rig's own space.</summary>
        private static readonly Dictionary<Side, Vector3> _lockControllerStart = new Dictionary<Side, Vector3>();

        private static void RememberControllers(Item item)
        {
            _lockControllerStart.Clear();
            if (Player.local == null)
            {
                return;
            }
            foreach (var hand in item.handlers)
            {
                var playerHand = hand != null ? hand.playerHand : null;
                if (playerHand != null && playerHand.grip != null && hand.creature != null && hand.creature.isPlayer)
                {
                    _lockControllerStart[hand.side] = Player.local.transform.InverseTransformPoint(playerHand.grip.position);
                }
            }
        }

        /// <summary>Where the blades crossed, on the enemy's blade as it is now (the frozen blade still settles a little).</summary>
        private static Vector3 LockPointNow() => _lockFlash.Weapon.transform.TransformPoint(_lockOnEnemyBlade);

        /// <summary>The enemy's push in the weapon hand (haptics), every 0.05 s.</summary>
        private static void Feel(float now)
        {
            var enemy = _enemyPeak > 0.0001f ? _rounds.EnemyPush / _enemyPeak : 0f;
            if (now >= _nextHaptic)
            {
                _nextHaptic = now + 0.05f;
                if (_rounds.Phase == ClashPhase.Push)
                {
                    Pulse(0.2f + (0.6f * enemy));
                }
            }
        }

        private static void Pulse(float strength)
        {
            if (_lockHand != null && Haptics > 0f)
            {
                _lockHand.HapticTick(Mathf.Clamp01(strength * Haptics));
            }
        }

        /// <summary>Headset diagnostic: the round, how far the controllers moved since the lock, the enemy's push, who is ahead, the blades' distance.</summary>
        private static void LogDiagnostic(float now, float lead, float side, float gap)
        {
            var apart = TryPlayerBlade(_lockHand, out var a, out var b) ? ClashGeometry.DistanceToSegment(V(LockPointNow()), V(a), V(b)) : float.NaN;
            Dmcs.Log.Debug(Channel, "lock t=" + F(Lock.Elapsed(now)) + " s, round " + _rounds.Round + "/" + _rounds.Rounds
                + ": controllers moved " + F(gap) + " m (forward " + F(lead) + " m, side " + F(side) + " m" + (_rounds.PushingSideways ? ", sideways" : "")
                + "), enemy push " + F(_rounds.EnemyPush) + " m, sums you "
                + F3(_rounds.PlayerSum) + " / enemy " + F3(_rounds.EnemySum) + ", advantage " + F(_rounds.Advantage) + ", blades "
                + (float.IsNaN(apart) ? "n/a" : F(apart) + " m") + " apart");
        }

        /// <summary>
        /// Clash Outcome Cues (user 2026-11-22: "unclear whether you win or lose"). Won: sparks thrown around the crossed
        /// blades, the arena's melee-kill stinger and three strong beats. Lost: a heavy thud and a long weaker rumble.
        /// </summary>
        private static void Cue(bool won, Vector3 point, RagdollHand hand)
        {
            if (!OutcomeCues)
            {
                return;
            }
            if (won)
            {
                for (var i = 0; i < ClashFightSounds.WinSparks; i++)
                {
                    Spark(point + (UnityEngine.Random.insideUnitSphere * ClashFightSounds.WinSparkSpread));
                }
            }
            OneShotEffect.Play(won ? ClashFightSounds.WinEffectId : ClashFightSounds.LoseEffectId, point);
            if (hand == null || Haptics <= 0f)
            {
                return;
            }
            var now = Time.unscaledTime;
            foreach (var (time, strength) in ClashDuelSignal.Haptics(won))
            {
                CueTicks.Add((now + time, strength, hand));
            }
        }

        /// <summary>Plays the due ticks of an outcome cue (they run on after the lock has ended).</summary>
        private static void PlayCueTicks()
        {
            if (CueTicks.Count == 0)
            {
                return;
            }
            var now = Time.unscaledTime;
            for (var i = CueTicks.Count - 1; i >= 0; i--)
            {
                var (at, strength, hand) = CueTicks[i];
                if (now < at)
                {
                    continue;
                }
                if (hand != null)
                {
                    hand.HapticTick(Mathf.Clamp01(strength * Haptics));
                }
                CueTicks.RemoveAt(i);
            }
        }

        /// <summary>
        /// The player won the lock: the enemy's sword flies away (Win Disarms), then it kneels with the kneel chance (the
        /// execution window of 4c opens) or staggers; Clash Win adrenaline.
        /// </summary>
        private static void Win()
        {
            var npc = _lockFlash.Npc;
            var hand = _lockFlash.Hand;
            var name = _lockFlash.NpcName;
            var rounds = _rounds.Rounds;
            var sideways = SidePush && _rounds.Sideways;
            var sideSign = _rounds.SideSign;
            Cue(won: true, LockPointNow(), _lockHand);
            EndLock(Lock.Decide(), "you won");
            var away = AwayFromPlayer(npc);
            string outcome;
            // User 2026-11-22: a duel won mostly sideways always disarms, the sword flung to that side; a forward win
            // disarms only with Clash Win Disarms. A disarmed enemy gets the disarm reaction, not a stagger on top (a
            // stagger push would end the game's cowering at once).
            var disarm = sideways || WinDisarms;
            var fling = sideways ? SideOf(sideSign) : away;
            var disarmed = disarm && DisarmStrike.DisarmAlong(npc, hand, fling, sideways ? "won clash sideways" : "won clash");
            PlayDarkMessiahDisarm(disarmed);
            if (disarmed)
            {
                outcome = sideways ? "you wrenched its sword aside to the " + (sideSign < 0 ? "left" : "right") : "its sword flies away";
            }
            else
            {
                var kneels = Dice.NextDouble() * 100.0 < WinKneelChance && AdrenalineCritical.KneelAlive(npc, KneelReason.ClashWin);
                if (!kneels && npc != null && !npc.isKilled)
                {
                    npc.ForceStagger(away, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium);
                }
                outcome = (disarm ? "no sword to knock away, " : "") + (kneels ? "it kneels" : "it staggers");
            }
            Dmcs.Log.Debug(Channel, "you won the clash against " + name + " after " + rounds + " round" + (rounds == 1 ? "" : "s")
                + (sideways ? " pushing sideways" : " pushing forward") + ": " + outcome);
            Adrenaline.Add(AdrenalineSource.ClashWin, name);
        }

        /// <summary>The player's right (sign 1) or left (−1), level, a little up: where a sideways win flings the sword.</summary>
        private static Vector3 SideOf(int sign)
        {
            var head = Player.local != null && Player.local.head != null ? Player.local.head.transform : null;
            var right = head != null ? head.right : Vector3.right;
            right.y = 0f;
            return ((right.normalized * (sign < 0 ? -1f : 1f)) + (Vector3.up * 0.3f)).normalized;
        }

        /// <summary>The player lost the lock: per Lose Outcome the weapon flies away and/or the player takes the hit; the enemy swings on.</summary>
        private static void Lose(string why, bool disarmPlayer)
        {
            var npc = _lockFlash?.Npc;
            var name = _lockFlash?.NpcName ?? "the enemy";
            var hand = _lockHand;
            var item = _lockItem;
            if (_lockFlash != null && _lockFlash.Weapon != null)
            {
                Cue(won: false, LockPointNow(), hand);
            }
            EndLock(Lock.Decide(), "you lost");
            var parts = new List<string>();
            // Every player hand on the weapon lets go: since the clash needs both hands (2026-11-24), releasing only the main
            // hand left the sword in the other one and the fling pushed a held sword (film 2026-11-24).
            var released = 0;
            if (disarmPlayer && ClashLoseOutcomes.Disarms(LoseOutcome) && item != null)
            {
                foreach (var holder in item.handlers.ToList())
                {
                    if (holder != null && holder.creature != null && holder.creature.isPlayer)
                    {
                        holder.TryRelease();
                        released++;
                    }
                }
            }
            if (released > 0)
            {
                if (item.physicBody != null && npc != null)
                {
                    var flight = item.transform.position - npc.transform.position;
                    flight.y = 0.3f * flight.magnitude;
                    item.physicBody.AddForce(flight.normalized * Mathf.Max(DisarmStrike.FlingForce, 2f), ForceMode.VelocityChange);
                }
                parts.Add("your " + item.data?.id + " flies away (" + released + (released == 1 ? " hand" : " hands") + " let go)");
            }
            var player = Player.currentCreature;
            if (ClashLoseOutcomes.Hits(LoseOutcome) && LoseDamage > 0f && player != null && !player.isKilled)
            {
                var before = player.currentHealth;
                player.Damage(LoseDamage, DamageType.Blunt);
                parts.Add("you take " + LoseDamage.ToString("0", CultureInfo.InvariantCulture) + " damage (health " + F(before) + " -> " + F(player.currentHealth) + ")");
            }
            Dmcs.Log.Debug(Channel, "you lost the clash against " + name + ": " + why + (parts.Count > 0 ? "; " + string.Join(", ", parts) : "; no penalty (" + LoseOutcome + ")"));
        }

        internal static Vector3 AwayFromPlayer(Creature npc)
        {
            var player = Player.currentCreature;
            if (npc == null || player == null)
            {
                return Vector3.up;
            }
            var away = npc.transform.position - player.transform.position;
            away.y = 0f;
            return (away.normalized + (Vector3.up * 0.5f)).normalized;
        }

        private static void EndLock(LockEnd end, string why)
        {
            if (end == LockEnd.None)
            {
                return;
            }
            var npc = _lockFlash?.Npc;
            if (npc != null && npc.animator != null)
            {
                npc.animator.speed = _lockAnimatorSpeed;
            }
            ReleaseHold();
            if (_lockItem != null && _lockItem.physicBody != null && _lockMass > 0f)
            {
                _lockItem.physicBody.mass = _lockMass;
            }
            Telegraph.Ended(_lockNpc);
            RemoveFlash(_lockNpc);
            Dmcs.Log.Debug(Channel, "blades unlocked (" + end + ": " + why + ")" + (npc != null ? ": " + npc.name + " moves again" : string.Empty));
            _lockFlash = null;
            _lockItem = null;
            _lockHand = null;
            _lockControllerStart.Clear();
            _rounds = null;
        }

        /// <summary>
        /// Hold Blade (user 2026-11-21): the player's blade stays where the blades met. A swing into the block used to carry it
        /// past the lock point (Player.log 2026-11-20, seven locks: up to 0.27–1.14 m off, mostly 0.2 s after the lock start), more so as the weight multiplies the
        /// moving blade's momentum. The swing's speed is taken out, then a joint keeps the touching point of the player's
        /// blade at the same point of the enemy's blade (user 2026-11-22: Hold Style Spring or Hard, Hold Rotation, Hold
        /// Briefly; rules in ClashHold). Free within Hold Slack; past it a hard stop or a spring sized on the blade's lock
        /// mass. The blade turns around that point unless Hold Rotation is on, and the real controller can lead the held hand
        /// (the push measure). Returns the log detail.
        /// </summary>
        private static string Hold(Item item, Item enemyWeapon, Vector3 contact)
        {
            var body = item.physicBody != null ? item.physicBody.rigidBody : null;
            if (!HoldBlade || body == null || enemyWeapon == null)
            {
                return "";
            }
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            _hold = body.gameObject.AddComponent<ConfigurableJoint>();
            _hold.autoConfigureConnectedAnchor = false;
            _hold.anchor = body.transform.InverseTransformPoint(contact);
            _hold.connectedAnchor = contact;
            var motion = ClashHold.IsRigid(HoldStyle, HoldSlack) ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Limited;
            _hold.xMotion = motion;
            _hold.yMotion = motion;
            _hold.zMotion = motion;
            _hold.linearLimit = new SoftJointLimit { limit = HoldSlack };
            var soft = ClashHold.IsSoft(HoldStyle);
            if (soft)
            {
                // Unity: a limit spring above zero makes the limit elastic (past the slack the spring pulls the blade back).
                var (spring, damper) = ClashHold.SpringFor(body.mass, HoldSpring, HoldSpringDamping);
                _hold.linearLimitSpring = new SoftJointLimitSpring { spring = spring, damper = damper };
            }
            var turning = HoldRotation ? ConfigurableJointMotion.Locked : ConfigurableJointMotion.Free;
            _hold.angularXMotion = turning;
            _hold.angularYMotion = turning;
            _hold.angularZMotion = turning;
            return ", held at the lock point (" + HoldStyle + (soft ? " " + F(HoldSpring) + "/" + F(HoldSpringDamping) : "") + ", slack " + F(HoldSlack) + " m"
                + (HoldRotation ? ", no turning" : "") + (HoldBriefly ? ", for " + F(HoldTime) + " s" : "") + ")";
        }

        private static void ReleaseHold()
        {
            if (_hold != null)
            {
                UnityEngine.Object.Destroy(_hold);
            }
            _hold = null;
        }

        /// <summary>Sparks (and the game's metal clang) at a point: Blade &amp; Sorcery's HitMaterialMetal effect, without its rubbing loop.</summary>
        private static void Spark(Vector3 point)
        {
            OneShotEffect.Play(ClashFightSounds.SparkEffectId, point);
        }

        /// <summary>Dark Messiah's "Sword_Versus_Sword" (a random take, volume 0.5–0.7, pitch 95–105 %) when switched on and found.</summary>
        private static void PlayDarkMessiahVersus()
        {
            if (!DarkMessiahSounds || !DarkMessiahAssets.TryClashSounds(out var versus, out _) || versus.Length == 0)
            {
                return;
            }
            var take = versus[Dice.Next(versus.Length)];
            PlayWav(take, 0.5f + ((float)Dice.NextDouble() * 0.2f), 0.95f + ((float)Dice.NextDouble() * 0.1f));
        }

        /// <summary>Dark Messiah's "Sword_Disarm" (volume 0.6) when switched on, found, and a sword flew.</summary>
        private static void PlayDarkMessiahDisarm(bool disarmed)
        {
            if (disarmed && DarkMessiahSounds && DarkMessiahAssets.TryClashSounds(out _, out var disarm) && disarm != null)
            {
                PlayWav(disarm, 0.6f, 1f);
            }
        }

        private static void PlayWav(WavData wav, float volume, float pitch)
        {
            if (!DarkMessiahClips.TryGetValue(wav, out var clip) || clip == null)
            {
                clip = AudioClip.Create("DMCS Clash (Dark Messiah)", wav.Samples.Length / wav.Channels, wav.Channels, wav.Rate, false);
                clip.SetData(wav.Samples, 0);
                DarkMessiahClips[wav] = clip;
            }
            EnsureAudio();
            if (_darkMessiahAudio == null)
            {
                // Its own source: the pitch of a source bends every sound it is playing, the flash sound included.
                _darkMessiahAudio = _audioHost.AddComponent<AudioSource>();
                _darkMessiahAudio.spatialBlend = 0f;
                _darkMessiahAudio.playOnAwake = false;
            }
            _darkMessiahAudio.pitch = pitch;
            _darkMessiahAudio.PlayOneShot(clip, volume);
        }

        /// <summary>Builds every DMCS flash sound in the background (each takes a moment in the game's Mono), at level load.</summary>
        public static void Prewarm()
        {
            if (DarkMessiahSounds)
            {
                DarkMessiahAssets.Begin();
            }
            foreach (var style in FlashSound.Sounding)
            {
                Samples(style);
            }
        }

        private static System.Threading.Tasks.Task<float[]> Samples(string style)
        {
            if (!SoundTasks.TryGetValue(style, out var task))
            {
                var spec = FlashSound.Spec(style);
                task = System.Threading.Tasks.Task.Run(() => FlashSound.Generate(SoundRate, 1, spec));
                SoundTasks[style] = task;
            }
            return task;
        }

        /// <summary>The chosen DMCS sound and game sound, together; returns what played, for the log.</summary>
        private static string PlayFlashSound(Vector3 point)
        {
            var played = "";
            try
            {
                var style = FlashSoundStyle;
                if (FlashSound.Spec(style) != null)
                {
                    var task = Samples(style);
                    if (!task.IsCompleted)
                    {
                        played += ", sound " + style + " still building";
                    }
                    else
                    {
                        if (!SoundClips.TryGetValue(style, out var clip) || clip == null)
                        {
                            var samples = task.Result;
                            clip = AudioClip.Create("DMCS Clash Flash (" + style + ")", samples.Length / 2, 2, SoundRate, false);
                            clip.SetData(samples, 0);
                            SoundClips[style] = clip;
                        }
                        EnsureAudio();
                        _audio.PlayOneShot(clip, FlashSoundVolume);
                        played += ", sound " + style + " (" + clip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s)";
                    }
                }
                var effectId = ClashSettings.GameSoundEffect(FlashGameSound);
                if (effectId != null)
                {
                    played += OneShotEffect.Play(effectId, point)
                        ? ", game sound " + FlashGameSound
                        : ", game sound " + FlashGameSound + " missing (" + effectId + ")";
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash flash sound error: " + ex);
            }
            return played;
        }

        private static void EnsureAudio()
        {
            if (_audio != null)
            {
                return;
            }
            _audioHost = new GameObject("DMCS Clash Audio");
            UnityEngine.Object.DontDestroyOnLoad(_audioHost);
            _audio = _audioHost.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
        }

        private static GameObject NewGlint()
        {
            if (_material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    if (!_shaderMissingLogged)
                    {
                        Dmcs.Log.Info("Clash flash: shader '" + ShaderName + "' not found; no flash.");
                        _shaderMissingLogged = true;
                    }
                    return null;
                }
                _texture = new Texture2D(GlintTextureSize, GlintTextureSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "DMCS Clash Glint",
                };
                _texture.LoadRawTextureData(Glint.Pixels(GlintTextureSize));
                _texture.Apply(false, true);
                _material = new Material(shader) { name = "DMCS Clash Glint" };
                _material.SetFloat("_Surface", 1f);
                _material.SetFloat("_Blend", 2f);
                _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _material.SetFloat("_ZWrite", 0f);
                _material.SetFloat("_Cull", 0f);
                _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _material.SetTexture("_BaseMap", _texture);
                _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DMCS Clash Flash";
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * GlintSize;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.material.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f, 0f));
            return go;
        }

        private static void RemoveFlash(int id)
        {
            if (Flashes.TryGetValue(id, out var flash))
            {
                if (flash.Glint != null)
                {
                    UnityEngine.Object.Destroy(flash.Renderer.material);
                    UnityEngine.Object.Destroy(flash.Glint);
                }
                Flashes.Remove(id);
            }
        }

        private static Vec3 V(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string F3(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();

        private sealed class Flash
        {
            public Flash(Creature npc, Item weapon, RagdollHand hand, Vector3 local, GameObject glint, float startedAt)
            {
                Npc = npc;
                NpcName = npc.name;
                Weapon = weapon;
                Hand = hand;
                Local = local;
                Glint = glint;
                Renderer = glint.GetComponent<MeshRenderer>();
                StartedAt = startedAt;
            }

            public Creature Npc { get; }
            public string NpcName { get; }
            public Item Weapon { get; }
            public RagdollHand Hand { get; }
            public Vector3 Local { get; }
            public GameObject Glint { get; }
            public MeshRenderer Renderer { get; }
            public float StartedAt { get; }

            /// <summary>Game time (Time.time) the wind-up started, its length, and the top tracker (null = show at once).</summary>
            public float WindUpAt { get; set; }

            public float WindUpSeconds { get; set; }

            public SwingTop Top { get; set; }

            /// <summary>Real time the flash appeared; null while it waits for the swing top.</summary>
            public float? ShownAt { get; set; }

            /// <summary>The blade tip's height above the NPC's feet.</summary>
            public float? TipHeight()
            {
                Weapon.GetFurthestDamagerCollider(out _, out var tip);
                return tip != null ? tip.bounds.center.y - Npc.transform.position.y : (float?)null;
            }
            public string LastDetail { get; set; } = "";

            /// <summary>The swing plane's normal from the strike start (null until read).</summary>

            public Vector3 Point => Weapon.transform.TransformPoint(Local);
        }
    }
}
