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

        public static int[] GainValues() => Enumerable.Range(0, 51).ToArray();
    }
}
