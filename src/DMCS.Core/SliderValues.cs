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
            Default = @default;
        }

        public string Name { get; }
        public float Min { get; }
        public float Max { get; }
        public float Step { get; }
        public float Default { get; }

        public float[] Values() => SliderValues.Create(Min, Max, Step);
    }
}
