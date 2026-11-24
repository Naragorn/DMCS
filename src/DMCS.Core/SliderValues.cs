using System;

namespace DMCS.Core
{
    /// <summary>
    /// The value list of a float slider, built with the game's formula (ThunderRoad
    /// ModOptionFloat.CreateArray: count = ceil((end - start) / step + 1), value = start + i * step),
    /// so a default can be checked to be exactly one of them: the game looks the default up with
    /// Equals and falls back to the first value when it is not found.
    /// </summary>
    public static class SliderValues
    {
        public static float[] Create(float start, float end, float step)
        {
            var count = (int)Math.Ceiling(((end - start) / step) + 1f);
            var values = new float[count];
            for (var i = 0; i < count; i++)
            {
                values[i] = start + (i * step);
            }
            return values;
        }

        public static int IndexOf(float[] values, float value)
        {
            return Array.FindIndex(values, v => v.Equals(value));
        }
    }

    /// <summary>One float slider's range and default.</summary>
    public sealed class SliderSpec
    {
        public SliderSpec(string name, float min, float max, float step, float @default)
        {
            Name = name;
            Min = min;
            Max = max;
            Step = step;
            Default = Nearest(Values(), @default);
        }

        public string Name { get; }
        public float Min { get; }
        public float Max { get; }
        public float Step { get; }
        public float Default { get; }

        public float[] Values() => SliderValues.Create(Min, Max, Step);

        /// <summary>
        /// The value from the list nearest to the wanted default: the game finds the default with Equals, and in its
        /// runtime (Mono) 0.2 + 5 x 0.05 is another float than the literal 0.45 (harness 2026-11-24: the menu showed the
        /// first value, 0.2). Taking it from the list makes it the same float the menu holds.
        /// </summary>
        private static float Nearest(float[] values, float wanted)
        {
            var best = wanted;
            var gap = float.MaxValue;
            foreach (var v in values)
            {
                var d = Math.Abs(v - wanted);
                if (d < gap)
                {
                    gap = d;
                    best = v;
                }
            }
            return best;
        }
    }
}
