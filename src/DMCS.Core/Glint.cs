using System;

namespace DMCS.Core
{
    /// <summary>
    /// The clash flash: a white four-point star (soft core plus thin rays along the axes) drawn at startup, tinted by the
    /// material; and its brightness over time: a quick flash, then a steady glow that marks the point to block.
    /// </summary>
    public static class Glint
    {
        /// <summary>Seconds to the flash's peak.</summary>
        public const float PeakSeconds = 0.08f;

        /// <summary>Brightness the glint holds after the flash.</summary>
        public const float HoldLevel = 0.6f;

        /// <summary>Seconds the flash takes to settle to the hold level.</summary>
        public const float SettleSeconds = 0.25f;

        /// <summary>RGBA pixels, row by row: white, the shape in alpha.</summary>
        public static byte[] Pixels(int size)
        {
            var px = new byte[size * size * 4];
            var half = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Math.Abs(x - half) / half;
                    var dy = Math.Abs(y - half) / half;
                    var r = Math.Sqrt((dx * dx) + (dy * dy));
                    var core = Math.Exp(-r * r / 0.02);
                    var rayX = Math.Exp(-dy * dy / 0.0015) * Math.Pow(Math.Max(0.0, 1.0 - dx), 0.6);
                    var rayY = Math.Exp(-dx * dx / 0.0015) * Math.Pow(Math.Max(0.0, 1.0 - dy), 0.6);
                    var a = Math.Min(1.0, core + (0.8 * Math.Max(rayX, rayY)));
                    var i = ((y * size) + x) * 4;
                    px[i] = 255;
                    px[i + 1] = 255;
                    px[i + 2] = 255;
                    px[i + 3] = (byte)Math.Round(a * 255.0);
                }
            }
            return px;
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
            var t = Math.Min(1f, (seconds - PeakSeconds) / SettleSeconds);
            return 1f - ((1f - HoldLevel) * t);
        }
    }
}
