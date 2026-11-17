using System;

namespace DMCS.Core
{
    /// <summary>What the vignette frames look like (Mod Menu values).</summary>
    public readonly struct VignetteSpec
    {
        public VignetteSpec(int size, int frames, int streaks, float length, float hueDegrees)
        {
            Size = size;
            Frames = frames;
            Streaks = streaks;
            Length = length;
            HueDegrees = hueDegrees;
        }

        /// <summary>Pixels per side (square frames).</summary>
        public int Size { get; }

        public int Frames { get; }

        /// <summary>Streaks per frame.</summary>
        public int Streaks { get; }

        /// <summary>How far the streaks reach in from the edge, as a share of the half-width (0..1).</summary>
        public float Length { get; }

        /// <summary>Base colour: 0 = red, 60 = yellow; the bright cores of the streaks run up to 25 degrees more orange.</summary>
        public float HueDegrees { get; }
    }

    /// <summary>
    /// The Dark Messiah-style adrenaline vignette, drawn by DMCS itself: jagged red and orange streaks reaching in from
    /// the edges (thin radial smears, mostly dim red, a few bright orange), a black centre, and every frame with the streaks somewhere else so a loop of frames flickers.
    /// (Dark Messiah's own overlay, read in its files: 25 frames, 256×256, played at 25 fps, drawn additively; DMCS
    /// copies the technique, not the texture.) Frames are RGBA bytes, row by row; black = see-through when drawn additively.
    /// </summary>
    public static class VignetteFlipbook
    {
        private const float GlowLevel = 0.08f;

        public static byte[][] Generate(VignetteSpec spec, int seed)
        {
            if (spec.Size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "size");
            }
            if (spec.Frames <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "frames");
            }
            if (spec.Streaks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "streaks");
            }
            if (spec.Length <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spec), "length");
            }
            var frames = new byte[spec.Frames][];
            for (var f = 0; f < spec.Frames; f++)
            {
                frames[f] = Frame(spec, new XorShift((seed * 31337) + ((f + 1) * 7477)));
            }
            return frames;
        }

        /// <summary>The frame to show <paramref name="seconds"/> after the start, looping at <paramref name="fps"/>.</summary>
        public static int FrameAt(float seconds, float fps, int frameCount)
        {
            if (fps <= 0f || frameCount <= 0 || seconds <= 0f)
            {
                return 0;
            }
            return (int)((long)Math.Floor(seconds * fps) % frameCount);
        }

        private static byte[] Frame(VignetteSpec spec, XorShift rng)
        {
            var hot = HotSpots(rng);
            var streaks = new Streak[spec.Streaks];
            for (var s = 0; s < streaks.Length; s++)
            {
                streaks[s] = new Streak(rng, spec.Length, hot);
            }
            var glowStart = 1f - (spec.Length * 0.4f);
            var innerCut = 1f - (spec.Length * 1.2f);
            var glow = GlowLevel * (0.75f + (0.25f * rng.NextUnit()));
            var size = spec.Size;
            var radius = new float[size * size];
            var angle = new float[size * size];
            var value = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var nx = (((x + 0.5f) / size) * 2f) - 1f;
                    var ny = (((y + 0.5f) / size) * 2f) - 1f;
                    var i = (y * size) + x;
                    radius[i] = (float)Math.Sqrt((nx * nx) + (ny * ny));
                    angle[i] = (float)Math.Atan2(ny, nx);
                    value[i] = glow * SmoothStep(glowStart, 1.15f, radius[i]);
                }
            }
            // Each streak only touches the pixels in its own bounding box (the frame builds in a few ms, not seconds).
            foreach (var s in streaks)
            {
                s.Bounds(size, out var x0, out var y0, out var x1, out var y1);
                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var i = (y * size) + x;
                        value[i] += s.At(radius[i], angle[i]);
                    }
                }
            }
            var pixels = new byte[size * size * 4];
            for (var i = 0; i < value.Length; i++)
            {
                var v = value[i] * SmoothStep(innerCut, innerCut + (spec.Length * 0.3f), radius[i]);
                Write(pixels, i * 4, Math.Min(1f, v), spec.HueDegrees);
            }
            return pixels;
        }

        /// <summary>Red to orange: R ≥ G ≥ B always (hue clamped to 0..60 degrees), opaque.</summary>
        private static void Write(byte[] p, int i, float v, float hue)
        {
            var h = Math.Max(0f, Math.Min(60f, hue + (25f * v * (float)Math.Sqrt(v))));
            var r = v;
            var g = v * (h / 60f);
            p[i] = ToByte(r);
            p[i + 1] = ToByte(g);
            p[i + 2] = 0;
            p[i + 3] = 255;
        }

        private static byte ToByte(float v) => (byte)Math.Round(Math.Max(0f, Math.Min(1f, v)) * 255f);

        private static float SmoothStep(float from, float to, float x)
        {
            var t = Math.Max(0f, Math.Min(1f, (x - from) / (to - from)));
            return t * t * (3f - (2f * t));
        }

        /// <summary>Two or three angles per frame where the streaks burn brighter (Dark Messiah's frames are patchy, not even).</summary>
        private static float[] HotSpots(XorShift rng)
        {
            var spots = new float[2 + (int)(rng.NextUnit() * 2f)];
            for (var i = 0; i < spots.Length; i++)
            {
                spots[i] = rng.NextUnit() * 2f * (float)Math.PI;
            }
            return spots;
        }

        /// <summary>0.35 far from every hot spot, up to 1 on one.</summary>
        private static float Cluster(float[] hot, float angle)
        {
            var best = 0f;
            foreach (var h in hot)
            {
                var d = Streak.Wrap(angle - h) / 0.6f;
                best = Math.Max(best, (float)Math.Exp(-(d * d)));
            }
            return 0.35f + (0.65f * best);
        }

        /// <summary>
        /// One streak: a thin, straight, radial smear (like a motion-blurred spark) somewhere in the outer band; soft along
        /// its length and across. Most are dim and red, few are bright and orange.
        /// </summary>
        private readonly struct Streak
        {
            private readonly float _angle;
            private readonly float _centre;
            private readonly float _halfLength;
            private readonly float _width;
            private readonly float _brightness;

            public Streak(XorShift rng, float length, float[] hot)
            {
                _angle = rng.NextUnit() * 2f * (float)Math.PI;
                _centre = 1.15f - (length * rng.NextUnit());
                _halfLength = length * (0.12f + (0.2f * rng.NextUnit()));
                _width = 0.015f + (0.035f * rng.NextUnit());
                var u = rng.NextUnit();
                _brightness = (0.15f + (0.6f * u * u)) * Cluster(hot, _angle);
            }

            /// <summary>Pixel box that holds everything this streak lights (3 sigma along and across).</summary>
            public void Bounds(int size, out int x0, out int y0, out int x1, out int y1)
            {
                var r0 = Math.Max(0f, _centre - (3f * _halfLength));
                var r1 = _centre + (3f * _halfLength);
                var minX = float.MaxValue;
                var minY = float.MaxValue;
                var maxX = float.MinValue;
                var maxY = float.MinValue;
                for (var k = 0; k < 9; k++)
                {
                    var r = k / 3 == 0 ? r0 : (k / 3 == 1 ? (r0 + r1) / 2f : r1);
                    var a = _angle + ((k % 3) - 1) * 3f * _width;
                    var px = (float)Math.Cos(a) * r;
                    var py = (float)Math.Sin(a) * r;
                    minX = Math.Min(minX, px);
                    minY = Math.Min(minY, py);
                    maxX = Math.Max(maxX, px);
                    maxY = Math.Max(maxY, py);
                }
                x0 = ToPixel(minX, size) - 1;
                y0 = ToPixel(minY, size) - 1;
                x1 = ToPixel(maxX, size) + 1;
                y1 = ToPixel(maxY, size) + 1;
                x0 = Math.Max(0, x0);
                y0 = Math.Max(0, y0);
                x1 = Math.Min(size - 1, x1);
                y1 = Math.Min(size - 1, y1);
            }

            private static int ToPixel(float n, int size) => (int)Math.Floor(((n + 1f) / 2f * size) - 0.5f);

            public float At(float r, float a)
            {
                var along = (r - _centre) / _halfLength;
                if (along * along > 9f)
                {
                    return 0f;
                }
                var d = Wrap(a - _angle) / _width;
                if (d * d > 9f)
                {
                    return 0f;
                }
                return _brightness * (float)Math.Exp(-(along * along) - (d * d));
            }

            public static float Wrap(float d)
            {
                const float tau = 2f * (float)Math.PI;
                d %= tau;
                if (d > Math.PI)
                {
                    d -= tau;
                }
                else if (d < -Math.PI)
                {
                    d += tau;
                }
                return d;
            }
        }
    }
}
