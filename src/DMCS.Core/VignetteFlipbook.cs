using System;

namespace DMCS.Core
{
    /// <summary>The three vignette drawings DMCS has built (2026-11-17), in the order they were made.</summary>
    public enum VignetteStyle
    {
        /// <summary>DMCS 1: jagged flame-like tongues from the edge, bright orange cores.</summary>
        Flames,

        /// <summary>DMCS 2: thin straight radial smears, evenly spread.</summary>
        Streaks,

        /// <summary>DMCS 3: thin radial smears, patchy (brighter around two or three spots per frame), closest to Dark Messiah.</summary>
        Patchy,
    }

    /// <summary>What the vignette frames look like (Mod Menu values).</summary>
    public readonly struct VignetteSpec
    {
        public VignetteSpec(int size, int frames, int streaks, float length, float hueDegrees, VignetteStyle style = VignetteStyle.Patchy)
        {
            Size = size;
            Frames = frames;
            Streaks = streaks;
            Length = length;
            HueDegrees = hueDegrees;
            Style = style;
        }

        /// <summary>Pixels per side (square frames).</summary>
        public int Size { get; }

        public int Frames { get; }

        /// <summary>Streaks per frame.</summary>
        public int Streaks { get; }

        /// <summary>How far the streaks reach in from the edge, as a share of the half-width (0..1).</summary>
        public float Length { get; }

        /// <summary>Base colour: 0 = red, 60 = yellow; bright cores run more orange.</summary>
        public float HueDegrees { get; }

        public VignetteStyle Style { get; }
    }

    /// <summary>
    /// The Dark Messiah-style adrenaline vignette, drawn by DMCS itself: red and orange streaks reaching in from the edges,
    /// a black centre, and every frame with the streaks somewhere else so a loop of frames flickers. (Dark Messiah's own
    /// overlay, read in its files: 25 frames, 256×256, played at 25 fps, drawn additively; DMCS copies the technique, not
    /// the texture.) Frames are RGBA bytes, row by row; black = see-through when drawn additively.
    /// </summary>
    public static class VignetteFlipbook
    {
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
            var flames = spec.Style == VignetteStyle.Flames;
            var hot = spec.Style == VignetteStyle.Patchy ? HotSpots(rng) : null;
            var streaks = new IStreak[spec.Streaks];
            for (var s = 0; s < streaks.Length; s++)
            {
                streaks[s] = flames ? (IStreak)new Flame(rng, spec.Length) : new Smear(rng, spec.Length, hot);
            }
            var glowStart = 1f - (spec.Length * 0.4f);
            var innerCut = 1f - (spec.Length * 1.2f);
            var glow = (flames ? 0.22f : 0.08f) * (0.75f + (0.25f * rng.NextUnit()));
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
                var v = flames ? value[i] : value[i] * SmoothStep(innerCut, innerCut + (spec.Length * 0.3f), radius[i]);
                Write(pixels, i * 4, Math.Min(1f, v), spec.HueDegrees, flames);
            }
            return pixels;
        }

        /// <summary>Red to orange: R ≥ G ≥ B always (hue clamped to 0..60 degrees), opaque.</summary>
        private static void Write(byte[] p, int i, float v, float hue, bool flames)
        {
            var shift = flames ? 30f * v * v : 25f * v * (float)Math.Sqrt(v);
            var h = Math.Max(0f, Math.Min(60f, hue + shift));
            p[i] = ToByte(v);
            p[i + 1] = ToByte(v * (h / 60f));
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

        /// <summary>0.35 far from every hot spot, up to 1 on one; 1 without hot spots.</summary>
        private static float Cluster(float[] hot, float angle)
        {
            if (hot == null)
            {
                return 1f;
            }
            var best = 0f;
            foreach (var h in hot)
            {
                var d = Wrap(angle - h) / 0.6f;
                best = Math.Max(best, (float)Math.Exp(-(d * d)));
            }
            return 0.35f + (0.65f * best);
        }

        private static float Wrap(float d)
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

        /// <summary>Pixel box around a radial band [r0, r1] and an angle ± half (corner and mid points).</summary>
        private static void Box(int size, float angle, float half, float r0, float r1, out int x0, out int y0, out int x1, out int y1)
        {
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            for (var k = 0; k < 9; k++)
            {
                var r = k / 3 == 0 ? r0 : (k / 3 == 1 ? (r0 + r1) / 2f : r1);
                var a = angle + (((k % 3) - 1) * half);
                var px = (float)Math.Cos(a) * r;
                var py = (float)Math.Sin(a) * r;
                minX = Math.Min(minX, px);
                minY = Math.Min(minY, py);
                maxX = Math.Max(maxX, px);
                maxY = Math.Max(maxY, py);
            }
            x0 = Math.Max(0, ToPixel(minX, size) - 1);
            y0 = Math.Max(0, ToPixel(minY, size) - 1);
            x1 = Math.Min(size - 1, ToPixel(maxX, size) + 1);
            y1 = Math.Min(size - 1, ToPixel(maxY, size) + 1);
        }

        private static int ToPixel(float n, int size) => (int)Math.Floor(((n + 1f) / 2f * size) - 0.5f);

        private interface IStreak
        {
            void Bounds(int size, out int x0, out int y0, out int x1, out int y1);

            float At(float r, float a);
        }

        /// <summary>
        /// DMCS 2 and 3: a thin, straight, radial smear (like a motion-blurred spark) somewhere in the outer band; soft along
        /// its length and across. Most are dim and red, few are bright and orange.
        /// </summary>
        private sealed class Smear : IStreak
        {
            private readonly float _angle;
            private readonly float _centre;
            private readonly float _halfLength;
            private readonly float _width;
            private readonly float _brightness;

            public Smear(XorShift rng, float length, float[] hot)
            {
                var patchy = hot != null;
                _angle = rng.NextUnit() * 2f * (float)Math.PI;
                _centre = 1.15f - (length * rng.NextUnit());
                _halfLength = patchy ? length * (0.12f + (0.2f * rng.NextUnit())) : length * (0.2f + (0.3f * rng.NextUnit()));
                _width = patchy ? 0.015f + (0.035f * rng.NextUnit()) : 0.012f + (0.025f * rng.NextUnit());
                var u = rng.NextUnit();
                _brightness = patchy ? (0.15f + (0.6f * u * u)) * Cluster(hot, _angle) : 0.25f + (0.75f * u * u);
            }

            public void Bounds(int size, out int x0, out int y0, out int x1, out int y1) =>
                Box(size, _angle, 3f * _width, Math.Max(0f, _centre - (3f * _halfLength)), _centre + (3f * _halfLength), out x0, out y0, out x1, out y1);

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
        }

        /// <summary>DMCS 1: a jagged tongue from the edge inwards, tapering and dimming towards its tip.</summary>
        private sealed class Flame : IStreak
        {
            private readonly float _angle;
            private readonly float _inner;
            private readonly float _length;
            private readonly float _width;
            private readonly float _brightness;
            private readonly float _jag;
            private readonly float _freq;
            private readonly float _phase1;
            private readonly float _phase2;

            public Flame(XorShift rng, float length)
            {
                _angle = rng.NextUnit() * 2f * (float)Math.PI;
                _length = length * (0.6f + (0.6f * rng.NextUnit()));
                _inner = 1f - _length;
                _width = 0.05f + (0.07f * rng.NextUnit());
                _brightness = 0.6f + (0.4f * rng.NextUnit());
                _jag = 0.04f + (0.05f * rng.NextUnit());
                _freq = 6f + (8f * rng.NextUnit());
                _phase1 = rng.NextUnit() * 6.2832f;
                _phase2 = rng.NextUnit() * 6.2832f;
            }

            public void Bounds(int size, out int x0, out int y0, out int x1, out int y1) =>
                Box(size, _angle, (3f * _width) + (1.5f * _jag), Math.Max(0f, _inner), 1.5f, out x0, out y0, out x1, out y1);

            public float At(float r, float a)
            {
                if (r <= _inner)
                {
                    return 0f;
                }
                var t = Math.Min(1f, (r - _inner) / _length);
                var centre = _angle + (_jag * (float)Math.Sin((_freq * r) + _phase1)) + (0.5f * _jag * (float)Math.Sin((2.3f * _freq * r) + _phase2));
                var d = Wrap(a - centre);
                var w = _width * (0.15f + (0.85f * t));
                if (d * d > 9f * w * w)
                {
                    return 0f;
                }
                return _brightness * (float)Math.Exp(-(d / w) * (d / w)) * (float)Math.Pow(t, 1.3);
            }
        }
    }
}
