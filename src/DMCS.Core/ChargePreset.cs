using System;

namespace DMCS.Core
{
    public enum ChargePreset
    {
        Gamey,
        Realistic,
        Custom,
    }

    /// <summary>The Charge Stagger values a preset sets (each still adjustable in the Mod Menu).</summary>
    public sealed class ChargeValues
    {
        public ChargeValues(int chancePercent, float speedThreshold, float speedWindow, float minSwingSpeed, string strength, float duration)
        {
            ChancePercent = chancePercent;
            SpeedThreshold = speedThreshold;
            SpeedWindow = speedWindow;
            MinSwingSpeed = minSwingSpeed;
            Strength = strength;
            Duration = duration;
        }

        public int ChancePercent { get; }
        public float SpeedThreshold { get; }
        public float SpeedWindow { get; }
        public float MinSwingSpeed { get; }
        public string Strength { get; }
        public float Duration { get; }

        public ChargeValues With(int? chancePercent = null, float? speedThreshold = null, float? speedWindow = null, float? minSwingSpeed = null, string strength = null, float? duration = null)
        {
            return new ChargeValues(
                chancePercent ?? ChancePercent,
                speedThreshold ?? SpeedThreshold,
                speedWindow ?? SpeedWindow,
                minSwingSpeed ?? MinSwingSpeed,
                strength ?? Strength,
                duration ?? Duration);
        }

        public bool SameAs(ChargeValues other)
        {
            return other != null
                && ChancePercent == other.ChancePercent
                && SpeedThreshold.Equals(other.SpeedThreshold)
                && SpeedWindow.Equals(other.SpeedWindow)
                && MinSwingSpeed.Equals(other.MinSwingSpeed)
                && string.Equals(Strength, other.Strength, StringComparison.Ordinal)
                && Duration.Equals(other.Duration);
        }
    }

    public static class ChargePresets
    {
        public static readonly string[] Names = { "Gamey", "Realistic", "Custom" };

        /// <summary>
        /// Easy to trigger: a fast jog is a charge (walk measured 2.76 m/s, sprint 6.0), the sprint counts for half a
        /// second, a light swing is enough, full stagger every time.
        /// </summary>
        public static readonly ChargeValues Gamey = new ChargeValues(100, 4f, 0.5f, 1f, "Full", 2.5f);

        /// <summary>
        /// Needs a real sprint at the moment of contact and a committed swing; a medium stagger, shorter, not every time.
        /// Swing speeds are not measured yet (headset check).
        /// </summary>
        public static readonly ChargeValues Realistic = new ChargeValues(75, 5.5f, 0.15f, 3f, "Medium", 1.25f);

        public static string NameOf(ChargePreset preset)
        {
            return Names[(int)preset];
        }

        public static ChargePreset Parse(string name)
        {
            var index = Array.IndexOf(Names, name);
            return index < 0 ? ChargePreset.Custom : (ChargePreset)index;
        }

        /// <summary>The values a preset sets; Custom has none.</summary>
        public static ChargeValues ValuesOf(ChargePreset preset)
        {
            switch (preset)
            {
                case ChargePreset.Gamey: return Gamey;
                case ChargePreset.Realistic: return Realistic;
                default: return null;
            }
        }

        /// <summary>Which preset the current values are exactly; anything else is Custom.</summary>
        public static ChargePreset Detect(ChargeValues values)
        {
            if (Gamey.SameAs(values))
            {
                return ChargePreset.Gamey;
            }
            return Realistic.SameAs(values) ? ChargePreset.Realistic : ChargePreset.Custom;
        }
    }
}
