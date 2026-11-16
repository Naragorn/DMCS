using System.Linq;

namespace DMCS.Core
{
    /// <summary>Adrenaline's Mod Menu names, ranges and defaults (single source of truth). Defaults per CLAUDE.md 4b.</summary>
    public static class AdrenalineSettings
    {
        public const string MeterMaxName = "Adrenaline Meter Max";
        public const string WindowDurationName = "Adrenaline Window";
        public const string DecayPerSecondName = "Adrenaline Decay Per Second";
        public const string DecayDelayName = "Adrenaline Decay Delay";
        public const string GainHitName = "Adrenaline Per Hit";
        public const string GainBlockParryName = "Adrenaline Per Block Or Parry";
        public const string GainDamageTakenName = "Adrenaline Per Damage Taken";
        public const string GainChargeStaggerName = "Adrenaline Per Charge Stagger";
        public const string GainDisarmName = "Adrenaline Per Disarm";

        public static readonly SliderSpec MeterMax = new SliderSpec(MeterMaxName, 50f, 300f, 10f, 100f);

        /// <summary>Seconds the full meter waits for the critical (real time).</summary>
        public static readonly SliderSpec WindowDuration = new SliderSpec(WindowDurationName, 2f, 20f, 1f, 6f);

        /// <summary>Points per second a filling meter loses; 0 = never drains.</summary>
        public static readonly SliderSpec DecayPerSecond = new SliderSpec(DecayPerSecondName, 0f, 20f, 0.5f, 2f);

        /// <summary>Seconds after the last gain before the meter starts draining.</summary>
        public static readonly SliderSpec DecayDelay = new SliderSpec(DecayDelayName, 0f, 30f, 1f, 0f);

        public static readonly SliderSpec[] Sliders = { MeterMax, WindowDuration, DecayPerSecond, DecayDelay };

        public const int DefaultGainHit = 5;
        public const int DefaultGainBlockParry = 10;
        public const int DefaultGainDamageTaken = 5;
        public const int DefaultGainChargeStagger = 15;
        public const int DefaultGainDisarm = 15;

        public const string DamageTakenLowersName = "Damage Taken Lowers Adrenaline";
        public const string DecayEnabledName = "Adrenaline Decay";

        /// <summary>Damage taken takes "Per Damage Taken" points off instead of adding them.</summary>
        public const bool DefaultDamageTakenLowers = true;

        /// <summary>Off for now (user, 2026-11-16: off while testing).</summary>
        public const bool DefaultDecayEnabled = false;

        public const string CriticalTimeModeName = "Critical Time Mode";
        public const string HyperfocusScaleName = "Critical Hyperfocus Time Scale";
        public const string HyperfocusDurationName = "Critical Hyperfocus Duration";
        public const string SlowTimeScaleName = "Critical Slow Time Scale";
        public const string SlowTimeDurationName = "Critical Slow Time Duration";
        public const string NeedsHardSwingName = "Critical Needs Hard Swing";
        public const string MinSwingSpeedName = "Critical Min Swing Speed";
        public const string RangedCountsName = "Critical Thrown And Spells";
        public const string KillsName = "Critical Kills";
        public const string DismemberName = "Critical Dismember";
        public const string KnockbackName = "Critical Knockback";
        public const string BattleCryName = "Critical Battle Cry";
        public const string ChestStabKneelsName = "Chest Stab Kneels";

        public const string DefaultTimeMode = CriticalTimeModes.Hyperfocus;
        public const bool DefaultNeedsHardSwing = true;
        public const bool DefaultRangedCounts = false;
        public const bool DefaultKills = true;
        public const bool DefaultDismember = true;
        public const bool DefaultBattleCry = true;
        public const bool DefaultChestStabKneels = true;

        /// <summary>World speed in Hyperfocus mode (the game's Hyperfocus skill: 0.1).</summary>
        public static readonly SliderSpec HyperfocusScale = new SliderSpec(HyperfocusScaleName, 0.05f, 0.5f, 0.05f, 0.1f);

        /// <summary>World and player speed in Slow Time mode (the game's slow time spell: 0.5).</summary>
        public static readonly SliderSpec SlowTimeScale = new SliderSpec(SlowTimeScaleName, 0.25f, 0.75f, 0.25f, 0.5f);

        /// <summary>Real seconds the slow-motion lasts (a first guess: long enough to kick the kneeling enemy away).</summary>
        public static readonly SliderSpec HyperfocusDuration = new SliderSpec(HyperfocusDurationName, 0.5f, 10f, 0.5f, 3f);

        public static readonly SliderSpec SlowTimeDuration = new SliderSpec(SlowTimeDurationName, 0.5f, 10f, 0.5f, 3f);

        /// <summary>m/s of the hit; default = Charge Stagger's min swing speed (spec).</summary>
        public static readonly SliderSpec MinSwingSpeed = new SliderSpec(MinSwingSpeedName, 0f, 10f, 0.25f, ChargeStaggerSettings.MinSwingSpeed.Default);

        /// <summary>m/s every ragdoll part gets along the hit; 0 = none (a first guess).</summary>
        public static readonly SliderSpec Knockback = new SliderSpec(KnockbackName, 0f, 20f, 1f, 8f);

        public static readonly SliderSpec[] CriticalSliders =
            { HyperfocusScale, SlowTimeScale, HyperfocusDuration, SlowTimeDuration, MinSwingSpeed, Knockback };

        public static int[] GainValues() => Enumerable.Range(0, 51).ToArray();
    }
}
