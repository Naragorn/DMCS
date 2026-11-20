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
    /// Charge Stagger: a melee hit the player lands while sprinting staggers the NPC.
    /// Speed = the game's Locomotion.horizontalSpeed (m/s), fastest value in a short window before the hit.
    /// The stagger itself is the game's: Creature.ForceStagger (BrainModuleHitReaction.ForceStaggerBehaviour)
    /// with the chosen level. Its length is the game's animation; "Stagger Duration" is how long DMCS counts
    /// the NPC as staggered (Disarm Strike) and how long a knocked-down NPC is kept on the ground
    /// (Brain.AddNoStandUpModifier, which BrainModuleHitReaction.OnCycle checks before standing up).
    /// </summary>
    public static class ChargeStagger
    {
        private const string Channel = "Charge Stagger";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ChargeStagger, true, Dmcs.Log);

        private static readonly SpeedHistory Speeds = new SpeedHistory(ChargeStaggerSettings.HistoryRetention);
        private static readonly StaggerWindows<int> Windows = new StaggerWindows<int>();
        private static readonly Dictionary<int, Creature> HeldDown = new Dictionary<int, Creature>();
        private static bool _hooked;

        /// <summary>Raised for each charge stagger (Adrenaline gains from it).</summary>
        public static event Action<Creature> Staggered;

        [ModOption(
            name = DmcsCategories.ChargeStagger + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Melee hits landed while sprinting stagger the enemy.",
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.MenuEnabled;
            set => Toggle.Set(value);
        }

        private static string _preset = ChargeStaggerSettings.DefaultPreset;
        private static int _chance = ChargeStaggerSettings.DefaultChancePercent;
        private static float _threshold = ChargeStaggerSettings.SpeedThreshold.Default;
        private static float _window = ChargeStaggerSettings.SpeedWindow.Default;
        private static float _minSwing = ChargeStaggerSettings.MinSwingSpeed.Default;
        private static string _strength = ChargeStaggerSettings.DefaultStrength;
        private static float _duration = ChargeStaggerSettings.Duration.Default;
        private static bool _applyingPreset;
        private static bool _showingPreset;
        private static readonly System.Random Dice = new System.Random();

        /// <summary>
        /// Picking Gamey or Realistic sets the values below; changing any of them afterwards shows Custom.
        /// Saved, but not applied on load (ModOptionApplyOnPress): on load the saved values are applied and
        /// the preset row follows them, so a preset can never overwrite saved custom values.
        /// </summary>
        [ModOption(
            name = ChargeStaggerSettings.PresetName,
            tooltip = "Gamey: easy charges, full stagger every time. Realistic: a real sprint and a committed swing, shorter stagger, not always. Adjust any value below to make it Custom.",
            valueSourceName = nameof(PresetValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 1)]
        [ModOptionApplyOnPress]
        public static string Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                if (_showingPreset)
                {
                    return;
                }
                var values = ChargePresets.ValuesOf(ChargePresets.Parse(value));
                if (values != null)
                {
                    ApplyPreset(values);
                }
            }
        }

        [ModOption(
            name = ChargeStaggerSettings.ChanceName,
            tooltip = "Chance (%) that a charge hit staggers.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 2)]
        [ModOptionSlider]
        public static int Chance
        {
            get => _chance;
            set => SetValue(ref _chance, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.SpeedThresholdName,
            tooltip = "Minimum running speed (m/s) for a hit to count as a charge. Walking is about 2.8, a full sprint about 6.",
            valueSourceName = nameof(SpeedThresholdValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 3)]
        [ModOptionSlider]
        public static float SpeedThreshold
        {
            get => _threshold;
            set => SetValue(ref _threshold, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.SpeedWindowName,
            tooltip = "How far back (seconds) before the hit your top speed still counts. A charge slows down on contact.",
            valueSourceName = nameof(SpeedWindowValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 4)]
        [ModOptionSlider]
        public static float SpeedWindow
        {
            get => _window;
            set => SetValue(ref _window, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.MinSwingSpeedName,
            tooltip = "How fast (m/s) the weapon, fist or projectile must hit. Higher needs a committed swing.",
            valueSourceName = nameof(MinSwingSpeedValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 5)]
        [ModOptionSlider]
        public static float MinSwingSpeed
        {
            get => _minSwing;
            set => SetValue(ref _minSwing, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.RangedCountsName,
            tooltip = "Thrown weapons and spells that hit while you run also stagger.",
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 6)]
        public static bool RangedCounts { get; set; } = ChargeStaggerSettings.DefaultRangedCounts;

        [ModOption(
            name = ChargeStaggerSettings.StrengthName,
            tooltip = "How hard the enemy is staggered: the game's light, medium or full stagger, or knocked to the ground.",
            valueSourceName = nameof(StrengthValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 7)]
        public static string Strength
        {
            get => _strength;
            set => SetValue(ref _strength, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.DurationName,
            tooltip = "Seconds the enemy counts as staggered (for Disarm Strike and the knock-down). A knocked-down enemy stays down this long.",
            valueSourceName = nameof(DurationValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 8)]
        [ModOptionSlider]
        public static float Duration
        {
            get => _duration;
            set => SetValue(ref _duration, value);
        }

        [ModOption(
            name = ChargeStaggerSettings.SecondKnocksDownName,
            tooltip = "A charge hit on an enemy that is already staggered throws it to the ground.",
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 9)]
        public static bool SecondKnocksDown { get; set; } = ChargeStaggerSettings.DefaultSecondKnocksDown;

        public static ModOptionString[] PresetValues() =>
            ChargePresets.Names.Select(n => new ModOptionString(n, null, n)).ToArray();

        public static ModOptionInt[] ChanceOptionValues() =>
            ChargeStaggerSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture) + " %", v)).ToArray();

        public static ModOptionFloat[] SpeedThresholdValues() => Floats(ChargeStaggerSettings.SpeedThreshold, "0.00", " m/s");

        public static ModOptionFloat[] SpeedWindowValues() => Floats(ChargeStaggerSettings.SpeedWindow, "0.00", " s");

        public static ModOptionFloat[] MinSwingSpeedValues() => Floats(ChargeStaggerSettings.MinSwingSpeed, "0.00", " m/s");

        public static ModOptionFloat[] DurationValues() => Floats(ChargeStaggerSettings.Duration, "0.00", " s");

        public static ModOptionString[] StrengthValues() =>
            StaggerStrengths.Names.Select(n => new ModOptionString(n, null, n)).ToArray();

        private static ChargeValues Current =>
            new ChargeValues(_chance, _threshold, _window, _minSwing, _strength, _duration);

        private static void SetValue<T>(ref T field, T value)
        {
            field = value;
            if (!_applyingPreset)
            {
                ShowMatchingPreset();
            }
        }

        private static void ApplyPreset(ChargeValues values)
        {
            _applyingPreset = true;
            try
            {
                SetOption(ChargeStaggerSettings.ChanceName, values.ChancePercent, v => _chance = v);
                SetOption(ChargeStaggerSettings.SpeedThresholdName, values.SpeedThreshold, v => _threshold = v);
                SetOption(ChargeStaggerSettings.SpeedWindowName, values.SpeedWindow, v => _window = v);
                SetOption(ChargeStaggerSettings.MinSwingSpeedName, values.MinSwingSpeed, v => _minSwing = v);
                SetOption(ChargeStaggerSettings.StrengthName, values.Strength, v => _strength = v);
                SetOption(ChargeStaggerSettings.DurationName, values.Duration, v => _duration = v);
            }
            finally
            {
                _applyingPreset = false;
            }
            Dmcs.Log.Debug(Channel, "preset " + _preset + " applied");
            ShowMatchingPreset();
        }

        /// <summary>Through the ModOption when the mod is loaded (saved + page row), else straight into the field.</summary>
        private static void SetOption<T>(string name, T value, Action<T> fallback)
        {
            if (!OptionSync.Apply(name, value))
            {
                fallback(value);
            }
        }

        /// <summary>The preset row shows which preset the current values are (Custom if none).</summary>
        private static void ShowMatchingPreset()
        {
            var detected = ChargePresets.NameOf(ChargePresets.Detect(Current));
            if (detected == _preset)
            {
                return;
            }
            _showingPreset = true;
            try
            {
                if (!OptionSync.Apply(ChargeStaggerSettings.PresetName, detected))
                {
                    _preset = detected;
                }
            }
            finally
            {
                _showingPreset = false;
            }
        }

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();

        /// <summary>True while the NPC is inside a charge stagger window.</summary>
        public static bool IsStaggered(Creature creature)
        {
            return creature != null && Windows.IsStaggered(creature.GetInstanceID(), Time.time);
        }

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (!_hooked)
            {
                EventManager.onCreatureHit += OnCreatureHit;
                EventManager.onCreatureDespawn += OnCreatureDespawn;
                _hooked = true;
            }
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureHit -= OnCreatureHit;
                EventManager.onCreatureDespawn -= OnCreatureDespawn;
                _hooked = false;
            }
            ReleaseAll();
        }

        /// <summary>Level unloading: nothing of this level may stay held.</summary>
        public static void ReleaseAll()
        {
            foreach (var id in Windows.TakeAll())
            {
                Release(id);
            }
            Speeds.Clear();
        }

        /// <summary>Every frame: sample the player's speed, end expired windows.</summary>
        public static void Update()
        {
            var now = Time.time;
            if (Toggle.Enabled)
            {
                var locomotion = Player.local != null ? Player.local.locomotion : null;
                if (locomotion != null)
                {
                    Speeds.Add(now, locomotion.horizontalSpeed);
                }
            }
            foreach (var id in Windows.TakeExpired(now))
            {
                Release(id);
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ReleaseAll();
            }
        }

        private static void OnCreatureHit(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            try
            {
                Handle(target, hit, eventTime);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Charge Stagger error: " + ex);
            }
        }

        private static void Handle(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            if (target == null || hit == null)
            {
                return;
            }
            if (eventTime == EventTime.OnStart)
            {
                HitInfo.RecordStart(target, hit);
            }
            var now = Time.time;
            var id = target.GetInstanceID();
            var peak = Speeds.Peak(now, SpeedWindow);
            var impact = hit.impactVelocity.magnitude;
            var source = HitInfo.SourceOf(hit);
            var decision = ChargeStaggerRule.Decide(new ChargeHit(
                Toggle.Enabled,
                eventTime == EventTime.OnEnd,
                source,
                RangedCounts,
                target.isPlayer,
                target.isKilled,
                HitInfo.WasStaggeredBefore(target, hit),
                SecondKnocksDown,
                peak,
                SpeedThreshold,
                impact,
                MinSwingSpeed,
                (float)Dice.NextDouble(),
                Chance / 100f));
            if (ChargeStaggerRule.IsQuiet(decision))
            {
                return;
            }
            var facts = source + ", run " + peak.ToString("0.00", CultureInfo.InvariantCulture)
                + " m/s (min " + SpeedThreshold.ToString("0.00", CultureInfo.InvariantCulture)
                + "), swing " + impact.ToString("0.00", CultureInfo.InvariantCulture)
                + " m/s (min " + MinSwingSpeed.ToString("0.00", CultureInfo.InvariantCulture) + ")";
            if (decision != ChargeDecision.Stagger && decision != ChargeDecision.KnockDown)
            {
                Dmcs.Log.Debug(Channel, "no stagger on " + target.name + ": " + decision + " (" + facts + ")");
                return;
            }

            var strength = decision == ChargeDecision.KnockDown ? StaggerStrength.KnockDown : StaggerStrengths.Parse(Strength);
            var part = hit.damageStruct.hitRagdollPart != null ? hit.damageStruct.hitRagdollPart.type : RagdollPart.Type.Torso;
            target.ForceStagger(hit.impactVelocity, EffectOf(strength), part);
            Windows.Mark(id, now, Duration);
            if (StaggerStrengths.KnocksDown(strength) && target.brain != null)
            {
                target.brain.AddNoStandUpModifier(Toggle);
                HeldDown[id] = target;
            }
            Dmcs.Log.Debug(Channel, (decision == ChargeDecision.KnockDown ? "knocked down " : "staggered ") + target.name
                + " (" + part + "): " + StaggerStrengths.NameOf(strength) + ", " + Duration.ToString("0.00", CultureInfo.InvariantCulture)
                + " s (" + facts + ")");
            Staggered?.Invoke(target);
        }

        private static void OnCreatureDespawn(Creature creature, EventTime eventTime)
        {
            if (creature == null || eventTime != EventTime.OnStart)
            {
                return;
            }
            // Creatures are pooled: a despawned one must not come back still marked or held down.
            var id = creature.GetInstanceID();
            Windows.Remove(id);
            Release(id);
        }

        private static void Release(int id)
        {
            if (!HeldDown.TryGetValue(id, out var creature))
            {
                return;
            }
            HeldDown.Remove(id);
            if (creature != null && creature.brain != null)
            {
                creature.brain.RemoveNoStandUpModifier(Toggle);
            }
        }

        private static BrainModuleHitReaction.PushBehaviour.Effect EffectOf(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Light: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerLight;
                case StaggerStrength.Medium: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium;
                case StaggerStrength.KnockDown: return BrainModuleHitReaction.PushBehaviour.Effect.Destabilize;
                default: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerFull;
            }
        }
    }
}
