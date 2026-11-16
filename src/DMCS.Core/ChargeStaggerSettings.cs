using System.Linq;

namespace DMCS.Core
{
    /// <summary>Charge Stagger's Mod Menu names, ranges and defaults (single source of truth). Defaults = the Gamey preset.</summary>
    public static class ChargeStaggerSettings
    {
        public const string PresetName = "Charge Stagger Preset";
        public const string ChanceName = "Charge Stagger Chance";
        public const string SpeedThresholdName = "Charge Speed Threshold";
        public const string SpeedWindowName = "Charge Speed Window";
        public const string MinSwingSpeedName = "Charge Min Swing Speed";
        public const string RangedCountsName = "Charge Thrown And Spells Count";
        public const string StrengthName = "Charge Stagger Strength";
        public const string DurationName = "Charge Stagger Duration";
        public const string SecondKnocksDownName = "Charge Second Hit Knocks Down";

        public const string DefaultPreset = "Gamey";

        /// <summary>m/s, the game's Locomotion.horizontalSpeed (harness, Arena: walk 2.76, sprint 6.0).</summary>
        public static readonly SliderSpec SpeedThreshold = new SliderSpec(SpeedThresholdName, 1f, 8f, 0.25f, ChargePresets.Gamey.SpeedThreshold);

        /// <summary>Seconds before the impact in which the fastest speed counts (a charge slows at contact).</summary>
        public static readonly SliderSpec SpeedWindow = new SliderSpec(SpeedWindowName, 0f, 1f, 0.05f, ChargePresets.Gamey.SpeedWindow);

        /// <summary>m/s, |CollisionInstance.impactVelocity|: how hard the swing, punch or projectile hit.</summary>
        public static readonly SliderSpec MinSwingSpeed = new SliderSpec(MinSwingSpeedName, 0f, 10f, 0.25f, ChargePresets.Gamey.MinSwingSpeed);

        /// <summary>Seconds DMCS counts the NPC as staggered (and holds a knocked-down NPC on the ground).</summary>
        public static readonly SliderSpec Duration = new SliderSpec(DurationName, 0.5f, 5f, 0.25f, ChargePresets.Gamey.Duration);

        public static readonly SliderSpec[] Sliders = { SpeedThreshold, SpeedWindow, MinSwingSpeed, Duration };

        public const int DefaultChancePercent = 100;

        public static int[] ChanceValues() => Enumerable.Range(0, 21).Select(i => i * 5).ToArray();

        public static string DefaultStrength => ChargePresets.Gamey.Strength;

        public const bool DefaultRangedCounts = true;
        public const bool DefaultSecondKnocksDown = true;

        /// <summary>The menu defaults as one value set (they are the Gamey preset).</summary>
        public static ChargeValues Defaults =>
            new ChargeValues(DefaultChancePercent, SpeedThreshold.Default, SpeedWindow.Default, MinSwingSpeed.Default, DefaultStrength, Duration.Default);

        /// <summary>The speed history keeps at least the longest selectable window.</summary>
        public static float HistoryRetention => SpeedWindow.Max;
    }
}
