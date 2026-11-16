namespace DMCS.Core
{
    /// <summary>Charge Stagger's Mod Menu names, ranges and defaults (single source of truth).</summary>
    public static class ChargeStaggerSettings
    {
        public const string SpeedThresholdName = "Charge Speed Threshold";
        public const string SpeedWindowName = "Charge Speed Window";
        public const string StrengthName = "Charge Stagger Strength";
        public const string DurationName = "Charge Stagger Duration";

        /// <summary>
        /// m/s, the game's Locomotion.horizontalSpeed. Measured with BasHarness in the Arena
        /// (2026-11-16, default player): walking 2.78 m/s, full sprint 6.05 m/s. The default sits between.
        /// </summary>
        public static readonly SliderSpec SpeedThreshold = new SliderSpec(SpeedThresholdName, 1f, 8f, 0.25f, 4.5f);

        /// <summary>Seconds before the impact in which the fastest speed counts (a charge slows at contact).</summary>
        public static readonly SliderSpec SpeedWindow = new SliderSpec(SpeedWindowName, 0f, 1f, 0.05f, 0.25f);

        /// <summary>Seconds DMCS counts the NPC as staggered (and holds a knocked-down NPC on the ground).</summary>
        public static readonly SliderSpec Duration = new SliderSpec(DurationName, 0.5f, 5f, 0.25f, 2f);

        public const string DefaultStrength = "Full";

        public static readonly SliderSpec[] Sliders = { SpeedThreshold, SpeedWindow, Duration };

        /// <summary>The speed history keeps at least the longest selectable window.</summary>
        public static float HistoryRetention => SpeedWindow.Max;
    }
}
