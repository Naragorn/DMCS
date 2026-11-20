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
        private static EffectInstance _grind;
        private static readonly Dictionary<WavData, AudioClip> DarkMessiahClips = new Dictionary<WavData, AudioClip>();

        private static readonly ClashTelegraph<int> Telegraph = new ClashTelegraph<int>(ClashSettings.JudgeWindow);
        private static readonly Dictionary<int, Flash> Flashes = new Dictionary<int, Flash>();
        private static readonly ClashLock Lock = new ClashLock();

        /// <summary>Whether the blades are locked now, and where (for diagnostics and probes).</summary>
        internal static bool IsLocked => Lock.IsLocked;

        /// <summary>The blades are locked with this NPC right now.</summary>
        internal static bool IsLockedWith(Creature npc) => npc != null && Lock.IsLocked && _lockNpc == npc.GetInstanceID();

        internal static Vector3? LockPoint => Lock.IsLocked && _lockFlash != null && _lockFlash.Weapon != null ? _lockFlash.Point : (Vector3?)null;
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

        [ModOption(name = ClashFightSettings.DifficultyName, tooltip = "How hard enemies push back in a blade lock. 0: barely; 50: some; 100: twice as hard; 200: you hardly have a chance.", valueSourceName = nameof(DifficultyValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 14)]
        [ModOptionSlider]
        public static float Difficulty { get; set; } = ClashFightSettings.Difficulty.Default;

        [ModOption(name = ClashFightSettings.ScalesWithHeightName, tooltip = "Taller enemies than you push harder, smaller ones weaker (in proportion to their height), and smaller ones last fewer rounds.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 15)]
        public static bool ScalesWithHeight { get; set; } = ClashFightSettings.DefaultScalesWithHeight;

        [ModOption(name = ClashFightSettings.ScalesWithGenderName, tooltip = "Female enemies push weaker (by the reduction below) and last fewer rounds.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 16)]
        public static bool ScalesWithGender { get; set; } = ClashFightSettings.DefaultScalesWithGender;

        [ModOption(name = ClashFightSettings.FemaleStrengthName, tooltip = "How much weaker (percent) a female enemy pushes. Women have about 40-60 % of men's upper-body strength.", valueSourceName = nameof(FemaleStrengthValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 17)]
        [ModOptionSlider]
        public static float FemaleStrength { get; set; } = ClashFightSettings.FemaleStrength.Default;

        [ModOption(name = ClashFightSettings.MinRoundsName, tooltip = "Fewest rounds of a blade lock (each lock picks a random number between min and max).", valueSourceName = nameof(RoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 18)]
        [ModOptionSlider]
        public static float MinRounds { get; set; } = ClashFightSettings.MinRounds.Default;

        [ModOption(name = ClashFightSettings.MaxRoundsName, tooltip = "Most rounds of a blade lock.", valueSourceName = nameof(RoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 19)]
        [ModOptionSlider]
        public static float MaxRounds { get; set; } = ClashFightSettings.MaxRounds.Default;

        [ModOption(name = ClashFightSettings.FewerRoundsName, tooltip = "Rounds fewer against a smaller or a female enemy (while the matching scaling is on); at least one round stays.", valueSourceName = nameof(FewerRoundsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 20)]
        [ModOptionSlider]
        public static float FewerRounds { get; set; } = ClashFightSettings.FewerRounds.Default;

        [ModOption(name = ClashFightSettings.EnemyPushName, tooltip = "How far (m) your real hand must lead your heavy weapon towards the enemy to match its push at its strongest, at difficulty 50.", valueSourceName = nameof(EnemyPushValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 21)]
        [ModOptionSlider]
        public static float EnemyPush { get; set; } = ClashFightSettings.EnemyPush.Default;

        [ModOption(name = ClashFightSettings.DeadZoneName, tooltip = "Lead (m) of your hand that does not count as pushing: the heavy weapon's natural lag.", valueSourceName = nameof(DeadZoneValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 22)]
        [ModOptionSlider]
        public static float DeadZone { get; set; } = ClashFightSettings.DeadZone.Default;

        [ModOption(name = ClashFightSettings.PushPhaseName, tooltip = "Seconds of one push round; the enemy's push swells and ebbs once in it.", valueSourceName = nameof(PushPhaseValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 23)]
        [ModOptionSlider]
        public static float PushPhase { get; set; } = ClashFightSettings.PushPhase.Default;

        [ModOption(name = ClashFightSettings.ReturnWindowName, tooltip = "Seconds to bring your hand back to the blades after a round, or you lose.", valueSourceName = nameof(ReturnWindowValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 24)]
        [ModOptionSlider]
        public static float ReturnWindow { get; set; } = ClashFightSettings.ReturnWindow.Default;

        [ModOption(name = ClashFightSettings.ReturnToleranceName, tooltip = "How close (m) your hand must come back to your weapon to count as back at the blades.", valueSourceName = nameof(ReturnToleranceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 25)]
        [ModOptionSlider]
        public static float ReturnTolerance { get; set; } = ClashFightSettings.ReturnTolerance.Default;

        [ModOption(name = ClashFightSettings.WinDisarmsName, tooltip = "Winning a blade lock knocks the enemy's sword away.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 26)]
        public static bool WinDisarms { get; set; } = ClashFightSettings.DefaultWinDisarms;

        [ModOption(name = ClashFightSettings.WinKneelChanceName, tooltip = "Chance (percent) that a beaten enemy goes down on its knees and you can execute it. Otherwise it staggers.", valueSourceName = nameof(WinKneelChanceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 27)]
        [ModOptionSlider]
        public static float WinKneelChance { get; set; } = ClashFightSettings.WinKneelChance.Default;

        [ModOption(name = ClashFightSettings.LoseOutcomeName, tooltip = "What a lost blade lock costs you: your weapon flies away, you take a hit, both, or nothing.", valueSourceName = nameof(LoseOutcomeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 28)]
        public static string LoseOutcome { get; set; } = ClashFightSettings.DefaultLoseOutcome;

        [ModOption(name = ClashFightSettings.LoseDamageName, tooltip = "Health you lose with the hit of a lost blade lock (you have 100).", valueSourceName = nameof(LoseDamageValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 29)]
        [ModOptionSlider]
        public static float LoseDamage { get; set; } = ClashFightSettings.LoseDamage.Default;

        [ModOption(name = ClashFightSettings.HapticsName, tooltip = "Strength of the vibration in your weapon hand during a blade lock (it follows the enemy's push).", valueSourceName = nameof(HapticsValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 30)]
        [ModOptionSlider]
        public static float Haptics { get; set; } = ClashFightSettings.Haptics.Default;

        [ModOption(name = ClashFightSettings.DarkMessiahSoundsName, tooltip = "Dark Messiah's own blade lock and disarm sounds, read from your Dark Messiah install (never shipped). Falls back to Blade & Sorcery's sounds when it is not found.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 31)]
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

        public static ModOptionFloat[] ReturnWindowValues() => Floats(ClashFightSettings.ReturnWindow, "0.00", " s");

        public static ModOptionFloat[] ReturnToleranceValues() => Floats(ClashFightSettings.ReturnTolerance, "0.00", " m");

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

        [ModOption(name = ClashSettings.BlockReachName, tooltip = "How close (m) your blade must be to the flash for the slow-motion.", valueSourceName = nameof(BlockReachValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 6)]
        [ModOptionSlider]
        public static float BlockReach { get; set; } = ClashSettings.BlockReach.Default;

        [ModOption(name = ClashSettings.BlockAngleToleranceName, tooltip = "How far (degrees) your blade may be off a right angle to the enemy's swing. 90: any angle.", valueSourceName = nameof(BlockAngleToleranceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 7)]
        [ModOptionSlider]
        public static float BlockAngleTolerance { get; set; } = ClashSettings.BlockAngleTolerance.Default;

        [ModOption(name = ClashSettings.FlashAtTopName, tooltip = "The blade flashes when the enemy has raised it all the way, just before the strike, like sunlight caught on it. Off: as soon as the wind-up starts (more warning).", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 13)]
        public static bool FlashAtTop { get; set; } = ClashSettings.DefaultFlashAtTop;

        [ModOption(name = ClashSettings.PointToleranceName, tooltip = "How close (m) to the flash your block must hit the enemy's blade to lock the blades. Elsewhere it is an ordinary parry.", valueSourceName = nameof(PointToleranceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 8)]
        [ModOptionSlider]
        public static float PointTolerance { get; set; } = ClashSettings.PointTolerance.Default;

        [ModOption(name = ClashSettings.WeaponWeightName, tooltip = "How much heavier your weapon gets while the blades are locked, so you cannot just push through.", valueSourceName = nameof(WeaponWeightValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 9)]
        [ModOptionSlider]
        public static float WeaponWeight { get; set; } = ClashSettings.WeaponWeight.Default;

        public static ModOptionFloat[] PointToleranceValues() => Floats(ClashSettings.PointTolerance, "0.00", " m");

        public static ModOptionFloat[] WeaponWeightValues() => Floats(ClashSettings.WeaponWeight, "0", "x");

        [ModOption(name = ClashSettings.FlashSoundName, tooltip = "Sound when an enemy's blade flashes. DMCS 1 is modelled on Sekiro's perilous-attack cue; DMCS 2 is a short one, DMCS 3 a darker one, DMCS 4 a bright one. Combines with the game sound below.", valueSourceName = nameof(FlashSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 10)]
        public static string FlashSoundStyle { get; set; } = ClashSettings.DefaultFlashSound;

        [ModOption(name = ClashSettings.FlashGameSoundName, tooltip = "One of Blade & Sorcery's own sounds with the flash (Metal Hit also sparks). Plays together with the sound above.", valueSourceName = nameof(FlashGameSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 11)]
        public static string FlashGameSound { get; set; } = ClashSettings.DefaultFlashGameSound;

        [ModOption(name = ClashSettings.FlashSoundVolumeName, tooltip = "Volume of the DMCS flash sound.", valueSourceName = nameof(FlashSoundVolumeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 12)]
        [ModOptionSlider]
        public static float FlashSoundVolume { get; set; } = ClashSettings.FlashSoundVolume.Default;

        public static ModOptionString[] FlashSoundValues() => FlashSound.Styles.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionString[] FlashGameSoundValues() => ClashSettings.GameSounds.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionFloat[] FlashSoundVolumeValues() => Floats(ClashSettings.FlashSoundVolume, "0.00", "");

        public static ModOptionFloat[] MinWindUpValues() => Floats(ClashSettings.MinWindUp, "0.00", " s");

        public static ModOptionFloat[] SlowMoScaleValues() => Floats(ClashSettings.SlowMoScale, "0.00", "x");

        public static ModOptionFloat[] NpcReachValues() => Floats(ClashSettings.NpcReach, "0.00", " m");

        public static ModOptionFloat[] BlockReachValues() => Floats(ClashSettings.BlockReach, "0.0", " m");

        public static ModOptionFloat[] BlockAngleToleranceValues() => Floats(ClashSettings.BlockAngleTolerance, "0", "°");

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
            EndTime(force: true);
        }

        public static void Update()
        {
            UpdateLock();
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
                flash.Glint.transform.position = flash.Point;
                flash.Glint.transform.localScale = Vector3.one * (GlintSize * Glint.Scale(age));
                if (cam != null)
                {
                    flash.Glint.transform.rotation = Quaternion.LookRotation(flash.Glint.transform.position - cam.transform.position)
                        * Quaternion.Euler(0f, 0f, Glint.SpinDegrees(age));
                }
                flash.Renderer.material.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f, Glint.Pulse(age)));
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
                    Dmcs.Log.Debug(Channel, "no slow-motion for " + pair.Value.NpcName + "'s swing: " + over.Value);
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
            var point = flash.Point;
            var bladeDistance = float.PositiveInfinity;
            Vector3 bladeAxis = Vector3.zero;
            foreach (var hand in new[] { player.handLeft, player.handRight })
            {
                if (!TryPlayerBlade(hand, out var a, out var b))
                {
                    continue;
                }
                var d = ClashGeometry.DistanceToSegment(V(point), V(a), V(b));
                if (d < bladeDistance)
                {
                    bladeDistance = d;
                    bladeAxis = b - a;
                }
            }
            float? angle = null;
            var npcHandle = flash.Hand != null && flash.Hand.grabbedHandle != null ? flash.Hand.grabbedHandle.transform.position : flash.Weapon.transform.position;
            var velocity = flash.Weapon.GetItemPointVelocity(point, true);
            var normal = ClashGeometry.SwingPlaneNormal(V(point - npcHandle), V(velocity), ClashSettings.MinBladeSpeed);
            if (normal.HasValue)
            {
                flash.Normal = new Vector3(normal.Value.X, normal.Value.Y, normal.Value.Z);
            }
            if (normal.HasValue && bladeAxis != Vector3.zero)
            {
                angle = ClashGeometry.AngleToNormal(V(bladeAxis), normal.Value);
            }
            var verdict = ClashRules.JudgeSlowMo(npcDistance, NpcReach, bladeDistance, BlockReach, angle, BlockAngleTolerance);
            flash.LastDetail = "enemy " + F(npcDistance) + " m, blade " + (float.IsInfinity(bladeDistance) ? "none" : F(bladeDistance) + " m")
                + ", angle " + (angle.HasValue ? F(angle.Value) + "°" : "n/a");
            return verdict;
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
                if (Parry.TakesOverFromClash(parryingItem, out var handSpeed))
                {
                    // Spec section 6: a hard swing into the attack is a power parry, never a clash.
                    Dmcs.Log.Debug(Channel, "you parried " + flash.NpcName + "'s swing hard (hand " + F(handSpeed) + " m/s): a power parry, not a clash");
                    return;
                }
                var point = flash.Point;
                var contactToFlash = Vector3.Distance(hit.contactPoint, point);
                var hand = parryingItem.mainHandler;
                float? angle = null;
                if (hand != null && TryPlayerBlade(hand, out var a, out var b))
                {
                    var npcHandle = flash.Hand != null && flash.Hand.grabbedHandle != null ? flash.Hand.grabbedHandle.transform.position : flash.Weapon.transform.position;
                    var now = ClashGeometry.SwingPlaneNormal(V(point - npcHandle), V(flash.Weapon.GetItemPointVelocity(point, true)), ClashSettings.MinBladeSpeed);
                    var normal = now ?? (flash.Normal.HasValue ? V(flash.Normal.Value) : (Vec3?)null);
                    if (normal.HasValue)
                    {
                        angle = ClashGeometry.AngleToNormal(V(b - a), normal.Value);
                    }
                }
                var verdict = ClashRules.JudgeBlock(contactToFlash, PointTolerance, angle, BlockAngleTolerance);
                var detail = "contact " + F(contactToFlash) + " m from the flash, angle " + (angle.HasValue ? F(angle.Value) + "°" : "n/a");
                if (verdict != BlockVerdict.Clash)
                {
                    Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s swing: " + verdict + " (" + detail + "), an ordinary parry");
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
            var player = Player.currentCreature;
            var npcHeight = flash.Npc.GetHeight();
            var playerHeight = player != null ? player.GetHeight() : 0f;
            var female = flash.Npc.data != null && flash.Npc.data.gender == CreatureData.Gender.Female;
            var factor = ClashStrength.Factor(Difficulty, ScalesWithHeight, npcHeight, playerHeight, ScalesWithGender, female, FemaleStrength);
            var weaker = ClashStrength.IsWeaker(ScalesWithHeight, npcHeight, playerHeight, ScalesWithGender, female);
            var rounds = ClashStrength.Rounds(Mathf.RoundToInt(MinRounds), Mathf.RoundToInt(MaxRounds), Dice.NextDouble(), weaker, Mathf.RoundToInt(FewerRounds));
            _enemyPeak = EnemyPush * factor;
            _rounds = new ClashRounds(rounds, PushPhase, ReturnWindow, _enemyPeak, DeadZone, ReturnTolerance);
            _lastTick = Time.unscaledTime;
            _nextHaptic = 0f;
            Lock.Start(Time.unscaledTime);
            Spark(contact);
            PlayDarkMessiahVersus();
            StartGrind(contact);
            Dmcs.Log.Debug(Channel, "blades locked with " + flash.NpcName + " (" + detail + "): enemy frozen, your " + item.data?.id + " "
                + WeaponWeight.ToString("0", CultureInfo.InvariantCulture) + "x heavier; " + rounds + " round" + (rounds == 1 ? "" : "s")
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
            var now = Time.unscaledTime;
            var dt = now - _lastTick;
            _lastTick = now;
            Measure(out var lead, out var gap);
            var step = _rounds.Tick(dt, lead, gap);
            Feel(now, lead);
            if (Lock.DiagnosticDue(now))
            {
                LogDiagnostic(now, lead, gap);
            }
            switch (step)
            {
                case ClashStep.PushHeld:
                    Dmcs.Log.Debug(Channel, "round " + _rounds.Round + "/" + _rounds.Rounds + " held (you " + F3(_rounds.PlayerSum) + " vs the enemy "
                        + F3(_rounds.EnemySum) + " m·s): the enemy pushes back, bring your hand back to the blades within " + F(ReturnWindow) + " s");
                    Pulse(1f);
                    Spark(_lockFlash.Point);
                    PlayDarkMessiahVersus();
                    break;
                case ClashStep.Returned:
                    Dmcs.Log.Debug(Channel, "back at the blades (" + F(gap) + " m): round " + _rounds.Round + "/" + _rounds.Rounds + " starts");
                    break;
                case ClashStep.Won:
                    Win();
                    break;
                case ClashStep.LostPush:
                    Lose("the enemy pushed harder in round " + _rounds.Round + "/" + _rounds.Rounds + " (you " + F3(_rounds.PlayerSum) + " vs the enemy " + F3(_rounds.EnemySum) + " m·s)", disarmPlayer: true);
                    break;
                case ClashStep.LostReturn:
                    Lose("your hand did not come back to the blades within " + F(ReturnWindow) + " s (still " + F(gap) + " m off)", disarmPlayer: true);
                    break;
            }
        }

        /// <summary>
        /// How far the player's real controller (PlayerHand.grip, the target the hand follows) is ahead of the weapon hand
        /// (RagdollHand.grip, lagging behind the heavy weapon): towards the enemy (lead) and at all (gap).
        /// </summary>
        private static void Measure(out float lead, out float gap)
        {
            lead = gap = 0f;
            var playerHand = _lockHand != null ? _lockHand.playerHand : null;
            if (playerHand == null || playerHand.grip == null || _lockHand.grip == null || Player.currentCreature == null)
            {
                return;
            }
            var ahead = playerHand.grip.position - _lockHand.grip.position;
            var toEnemy = _lockFlash.Npc.transform.position - Player.currentCreature.transform.position;
            toEnemy.y = 0f;
            lead = toEnemy.sqrMagnitude > 0.0001f ? Vector3.Dot(ahead, toEnemy.normalized) : 0f;
            gap = ahead.magnitude;
        }

        /// <summary>The enemy's push in the weapon hand (haptics) and in the grinding sound, every 0.05 s.</summary>
        private static void Feel(float now, float lead)
        {
            var enemy = _enemyPeak > 0.0001f ? _rounds.EnemyPush / _enemyPeak : 0f;
            var mine = _enemyPeak > 0.0001f ? Mathf.Clamp01(_rounds.PlayerPush(lead) / _enemyPeak) : 0f;
            if (_grind != null)
            {
                _grind.SetIntensity(Mathf.Clamp01(0.3f + (0.5f * enemy) + (0.2f * mine)));
            }
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

        /// <summary>Headset diagnostic: the round, its phase, the lead and gap of the controller, the enemy's push, the blades' distance.</summary>
        private static void LogDiagnostic(float now, float lead, float gap)
        {
            var apart = TryPlayerBlade(_lockHand, out var a, out var b) ? ClashGeometry.DistanceToSegment(V(_lockFlash.Point), V(a), V(b)) : float.NaN;
            Dmcs.Log.Debug(Channel, "lock t=" + F(Lock.Elapsed(now)) + " s, round " + _rounds.Round + "/" + _rounds.Rounds + " " + _rounds.Phase
                + ": hand ahead " + F(gap) + " m (towards the enemy " + F(lead) + " m), enemy push " + F(_rounds.EnemyPush) + " m, sums you "
                + F3(_rounds.PlayerSum) + " / enemy " + F3(_rounds.EnemySum) + ", blades " + (float.IsNaN(apart) ? "n/a" : F(apart) + " m") + " apart");
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
            EndLock(Lock.Decide(), "you won");
            var away = AwayFromPlayer(npc);
            var disarmed = WinDisarms && DisarmStrike.DisarmAlong(npc, hand, away, "won clash");
            PlayDarkMessiahDisarm(disarmed);
            var kneels = Dice.NextDouble() * 100.0 < WinKneelChance && AdrenalineCritical.KneelAlive(npc, KneelReason.ClashWin);
            if (!kneels && npc != null && !npc.isKilled)
            {
                npc.ForceStagger(away, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium);
            }
            Dmcs.Log.Debug(Channel, "you won the clash against " + name + " after " + rounds + " round" + (rounds == 1 ? "" : "s") + ": "
                + (WinDisarms ? (disarmed ? "its sword flies away, " : "no sword to knock away, ") : "") + (kneels ? "it kneels" : "it staggers"));
            Adrenaline.Add(AdrenalineSource.ClashWin, name);
        }

        /// <summary>The player lost the lock: per Lose Outcome the weapon flies away and/or the player takes the hit; the enemy swings on.</summary>
        private static void Lose(string why, bool disarmPlayer)
        {
            var npc = _lockFlash?.Npc;
            var name = _lockFlash?.NpcName ?? "the enemy";
            var hand = _lockHand;
            var item = _lockItem;
            EndLock(Lock.Decide(), "you lost");
            var parts = new List<string>();
            if (disarmPlayer && ClashLoseOutcomes.Disarms(LoseOutcome) && hand != null && hand.grabbedHandle != null && hand.grabbedHandle.item == item)
            {
                hand.TryRelease();
                if (item != null && item.physicBody != null && npc != null)
                {
                    var flight = item.transform.position - npc.transform.position;
                    flight.y = 0.3f * flight.magnitude;
                    item.physicBody.AddForce(flight.normalized * Mathf.Max(DisarmStrike.FlingForce, 2f), ForceMode.VelocityChange);
                }
                parts.Add("your " + (item != null ? item.data?.id : "weapon") + " flies away");
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
            if (_lockItem != null && _lockItem.physicBody != null && _lockMass > 0f)
            {
                _lockItem.physicBody.mass = _lockMass;
            }
            StopGrind();
            Telegraph.Ended(_lockNpc);
            RemoveFlash(_lockNpc);
            Dmcs.Log.Debug(Channel, "blades unlocked (" + end + ": " + why + ")" + (npc != null ? ": " + npc.name + " moves again" : string.Empty));
            _lockFlash = null;
            _lockItem = null;
            _lockHand = null;
            _rounds = null;
        }

        /// <summary>Sparks (and the game's metal clang) at a point: Blade &amp; Sorcery's HitMaterialMetal effect.</summary>
        private static void Spark(Vector3 point)
        {
            var effect = Catalog.GetData<EffectData>(ClashFightSounds.SparkEffectId, false)?.Spawn(point, Quaternion.identity);
            effect?.Play();
        }

        /// <summary>The game's metal rubbing loop at the blades for the whole lock; its intensity follows the push.</summary>
        private static void StartGrind(Vector3 point)
        {
            StopGrind();
            _grind = Catalog.GetData<EffectData>(ClashFightSounds.GrindEffectId, false)?.Spawn(point, Quaternion.identity);
            if (_grind != null)
            {
                _grind.SetIntensity(0.3f);
                _grind.Play();
            }
        }

        private static void StopGrind()
        {
            if (_grind != null)
            {
                _grind.End();
                _grind = null;
            }
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
                    var effect = Catalog.GetData<EffectData>(effectId, false)?.Spawn(point, Quaternion.identity);
                    if (effect != null)
                    {
                        effect.Play();
                        played += ", game sound " + FlashGameSound;
                    }
                    else
                    {
                        played += ", game sound " + FlashGameSound + " missing (" + effectId + ")";
                    }
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
            public Vector3? Normal { get; set; }

            public Vector3 Point => Weapon.transform.TransformPoint(Local);
        }
    }
}
