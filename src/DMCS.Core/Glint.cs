using System;

namespace DMCS.Core
{
    /// <summary>
    /// The clash flash: a white star drawn at startup (bright core, soft halo, four long rays along the axes and four
    /// shorter, fainter diagonal rays), tinted by the material; and its life: a burst (bright and oversized), then a
    /// steady, slowly turning glow that marks the point to block. Picture probe 2026-11-17: the first version (thin
    /// rays, 0.14 m, 60 % glow) was hard to see from the player's eye.
    /// </summary>
    public static class Glint
    {
        /// <summary>Seconds to the burst's peak.</summary>
        public const float PeakSeconds = 0.08f;

        /// <summary>Seconds the burst takes to settle to the glow.</summary>
        public const float SettleSeconds = 0.25f;

        /// <summary>Brightness of the glow after the burst.</summary>
        public const float HoldLevel = 0.8f;

        /// <summary>Size factor at the start, at the burst's peak, and of the glow.</summary>
        public const float StartScale = 0.5f;
        public const float PeakScale = 1.5f;

        /// <summary>The star turns slowly so its rays glitter.</summary>
        public const float SpinDegreesPerSecond = 40f;

        /// <summary>RGBA pixels, row by row: white, the shape in alpha.</summary>
        public static byte[] Pixels(int size)
        {
            var px = new byte[size * size * 4];
            var half = (size - 1) / 2.0;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var u = (x - half) / half;
                    var v = (y - half) / half;
                    var r2 = (u * u) + (v * v);
                    var core = Math.Exp(-r2 / 0.004);
                    var halo = 0.35 * Math.Exp(-r2 / 0.05);
                    var main = Math.Max(Ray(u, v, 1.0, 0.00035), Ray(v, u, 1.0, 0.00035));
                    var d1 = (u + v) / Math.Sqrt(2.0);
                    var d2 = (u - v) / Math.Sqrt(2.0);
                    var diagonal = 0.55 * Math.Max(Ray(d1, d2, 0.6, 0.0003), Ray(d2, d1, 0.6, 0.0003));
                    var a = Math.Min(1.0, core + halo + main + diagonal);
                    var i = ((y * size) + x) * 4;
                    px[i] = 255;
                    px[i + 1] = 255;
                    px[i + 2] = 255;
                    px[i + 3] = (byte)Math.Round(a * 255.0);
                }
            }
            return px;
        }

        /// <summary>A ray along one axis: sharp across it, fading out along it to <paramref name="length"/>.</summary>
        private static double Ray(double along, double across, double length, double width)
        {
            var t = Math.Abs(along) / length;
            if (t >= 1.0)
            {
                return 0.0;
            }
            return Math.Exp(-(across * across) / width) * Math.Pow(1.0 - t, 0.8);
        }

        /// <summary>Brightness 0–1 at <paramref name="seconds"/> after the wind-up started.</summary>
        public static float Pulse(float seconds)
        {
            if (seconds <= 0f)
            {
                return 0f;
            }
            if (seconds <= PeakSeconds)
            {
                return seconds / PeakSeconds;
            }
            return Lerp(1f, HoldLevel, Settle(seconds));
        }

        /// <summary>Size factor at <paramref name="seconds"/>: from small to oversized at the peak, then back to 1.</summary>
        public static float Scale(float seconds)
        {
            if (seconds <= 0f)
            {
                return StartScale;
            }
            if (seconds <= PeakSeconds)
            {
                return Lerp(StartScale, PeakScale, seconds / PeakSeconds);
            }
            return Lerp(PeakScale, 1f, Settle(seconds));
        }

        public static float SpinDegrees(float seconds) => seconds * SpinDegreesPerSecond;

        private static float Settle(float seconds) => Math.Min(1f, (seconds - PeakSeconds) / SettleSeconds);

        private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
    }
}
