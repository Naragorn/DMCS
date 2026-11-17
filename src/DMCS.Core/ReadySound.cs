using System;

namespace DMCS.Core
{
    /// <summary>
    /// What the "adrenaline ready" sound is made of. Modelled on what Dark Messiah's own "Adrenaline_Reached"
    /// (game_sounds_inventory.txt, "// Full Adrenaline", volume 0.6) does, measured in its file (2026-11-17):
    /// 2.29 s stereo; a thunder roar at the start (dense, weighted to the low mids, swelling in over ~50 ms, loudest
    /// 0.05–0.3 s, rolling off to a quarter by 0.6 s); a low rumble (20–160 Hz) that carries the first 1.2 s and is gone at 1.7 s; a bright hiss
    /// (above 3.5 kHz) that swells in the first 0.2 s and fades by 1.2 s; and a metallic ring at about 1021 Hz with
    /// partials near ×2, ×3 and ×3.85 and sidebands about 60 Hz off, which swells to its loudest near 1 s and rings
    /// out to the end. DMCS builds its own sound from these traits (three versions, see the presets); it ships nothing of
    /// Dark Messiah's.
    /// </summary>
    public sealed class ReadySoundSpec
    {
        public float Duration { get; set; } = 2.3f;
        public float RingHz { get; set; } = 1021f;
        public float RumbleLevel { get; set; } = 0.6f;
        public float HissLevel { get; set; } = 0.07f;
        public float ThunderLevel { get; set; } = 0.25f;
        public float RingLevel { get; set; } = 0.005f;
        public float Peak { get; set; } = 0.9f;

        /// <summary>The rumble's loudness: base + |flutter| × depth, at most base + cap.</summary>
        public float RumbleFlutterBase { get; set; } = 0.75f;
        public float RumbleFlutterDepth { get; set; } = 8f;
        public float RumbleFlutterCap { get; set; } = 0.8f;
        public float RumbleDecay { get; set; } = 0.17f;

        /// <summary>Share of the hiss that stays until the body ends; 0 = it only decays (and is not cut with the body).</summary>
        public float HissFloor { get; set; } = 0.3f;

        public ThunderStyle Thunder { get; set; } = ThunderStyle.Roar;

        /// <summary>tanh drive that rounds off peaks before the final normalising; 0 = none.</summary>
        public float SoftLimitDrive { get; set; } = 1f;

        /// <summary>DMCS 1 (2026-11-17, first build): rumble, hiss and ring, no thunder.</summary>
        public static ReadySoundSpec Version1() => new ReadySoundSpec
        {
            RumbleLevel = 1f, ThunderLevel = 0f, Thunder = ThunderStyle.None, RumbleFlutterBase = 0.55f, RumbleFlutterDepth = 12f,
            RumbleFlutterCap = float.PositiveInfinity, RumbleDecay = 0.24f, HissFloor = 0f, SoftLimitDrive = 0f,
        };

        /// <summary>DMCS 2: with a crackling thunder crack at the start (the user found it too harsh).</summary>
        public static ReadySoundSpec Version2() => new ReadySoundSpec
        {
            RumbleLevel = 1f, Thunder = ThunderStyle.Crackle, RumbleFlutterBase = 0.55f, RumbleFlutterDepth = 12f,
            RumbleFlutterCap = float.PositiveInfinity, SoftLimitDrive = 1.5f,
        };

        /// <summary>DMCS 3: the thunder as a dense dark roar with a soft swell (the defaults).</summary>
        public static ReadySoundSpec Version3() => new ReadySoundSpec();
    }

    public enum ThunderStyle
    {
        None,

        /// <summary>Noise 50–2000 Hz in crackling bursts, linear 40 ms attack, held to 0.3 s.</summary>
        Crackle,

        /// <summary>Noise from 50 Hz falling off above ~700 Hz, slow swell, 60 ms curved attack, held to 0.4 s.</summary>
        Roar,
    }

    /// <summary>Synthesises the "adrenaline ready" sound (interleaved stereo floats, -1..1). Pure and deterministic per seed.</summary>
    public static class ReadySound
    {
        private static readonly float[] PartialRatios = { 1f, 2.0f, 3.0f, 3.855f, 4.9f, 6.1f };
        private static readonly float[] PartialLevels = { 1f, 1f, 0.2f, 0.15f, 0.1f, 0.08f };

        private const float RingPeakAt = 1.0f;
        private const float RingDecay = 0.42f;
        private const float RumbleHoldUntil = 1.1f;
        private const float HissPeakAt = 0.12f;
        private const float HissDecay = 0.28f;
        private const float ThunderDecay = 0.13f;
        private const float ThunderFloor = 0.22f;
        private const float FadeIn = 0.005f;
        private const float FadeOut = 0.06f;

        public static float[] Generate(int sampleRate, int seed, ReadySoundSpec spec)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            }
            var n = (int)Math.Round(spec.Duration * sampleRate);
            var output = new float[n * 2];
            var shared = Rumble(n, sampleRate, new XorShift(seed * 7919 + 17), spec);
            for (var ch = 0; ch < 2; ch++)
            {
                var rng = new XorShift((seed * 7919) + ((ch + 1) * 104729));
                var own = Rumble(n, sampleRate, rng, spec);
                var hiss = Hiss(n, sampleRate, rng);
                var thunder = spec.Thunder == ThunderStyle.None ? null : Thunder(n, sampleRate, rng, spec.Thunder);
                var ring = Ring(n, sampleRate, spec.RingHz * (ch == 0 ? 0.9993f : 1.0007f), rng);
                for (var i = 0; i < n; i++)
                {
                    var t = (float)i / sampleRate;
                    var rumble = ((0.4f * shared[i]) + (0.6f * own[i])) * RumbleEnvelope(t, spec.RumbleDecay);
                    var v = (spec.RumbleLevel * rumble) + (spec.HissLevel * hiss[i] * HissEnvelope(t, spec.HissFloor))
                        + (thunder == null ? 0f : spec.ThunderLevel * thunder[i] * ThunderEnvelope(t, spec.Thunder))
                        + (spec.RingLevel * ring[i]);
                    output[(2 * i) + ch] = v * Edges(i, n, sampleRate);
                }
            }
            if (spec.SoftLimitDrive > 0f)
            {
                Normalise(output, 1f);
                SoftLimit(output, spec.SoftLimitDrive);
            }
            Normalise(output, spec.Peak);
            return output;
        }

        /// <summary>Brown-ish noise below ~110 Hz with a fluttering loudness (the rumble's "growl").</summary>
        private static float[] Rumble(int n, int rate, XorShift rng, ReadySoundSpec spec)
        {
            var low = new OnePole(110f, rate);
            var low2 = new OnePole(110f, rate);
            var low3 = new OnePole(140f, rate);
            var flutter = new OnePole(14f, rate);
            var flutter2 = new OnePole(14f, rate);
            var x = new float[n];
            for (var i = 0; i < n; i++)
            {
                var body = low3.Next(low2.Next(low.Next(rng.NextSigned()))) * 1.6f;
                var am = flutter2.Next(flutter.Next(rng.NextSigned()));
                x[i] = body * (spec.RumbleFlutterBase + Math.Min(spec.RumbleFlutterCap, Math.Abs(am) * spec.RumbleFlutterDepth));
            }
            return x;
        }

        /// <summary>
        /// The thunder. Roar: a dense, dark roar (noise from ~50 Hz, falling off above ~700 Hz) that swells and sags slowly
        /// (Dark Messiah's start is a steady roar weighted to the low mids). Crackle: noise 50–2000 Hz in bursts.
        /// </summary>
        private static float[] Thunder(int n, int rate, XorShift rng, ThunderStyle style)
        {
            var roar = style == ThunderStyle.Roar;
            var high = new OnePole(50f, rate);
            var low = new OnePole(roar ? 700f : 2000f, rate);
            var low2 = new OnePole(roar ? 900f : 2000f, rate);
            var mod = new OnePole(roar ? 12f : 35f, rate);
            var x = new float[n];
            for (var i = 0; i < n; i++)
            {
                var w = rng.NextSigned();
                var band = low2.Next(low.Next(w - high.Next(w)));
                if (roar)
                {
                    var swell = Math.Abs(mod.Next(rng.NextSigned())) * 4f;
                    x[i] = band * (0.75f + Math.Min(0.6f, swell));
                }
                else
                {
                    var burst = Math.Abs(mod.Next(rng.NextSigned())) * 9f;
                    x[i] = band * (0.4f + burst);
                }
            }
            return x;
        }

        /// <summary>White noise above ~3 kHz.</summary>
        private static float[] Hiss(int n, int rate, XorShift rng)
        {
            var low = new OnePole(3000f, rate);
            var x = new float[n];
            for (var i = 0; i < n; i++)
            {
                var w = rng.NextSigned();
                x[i] = w - low.Next(w);
            }
            return x;
        }

        /// <summary>The metallic ring: partials with a slow beat (a twin 3 Hz off) and sidebands 62 Hz off.</summary>
        private static float[] Ring(int n, int rate, float hz, XorShift rng)
        {
            var x = new float[n];
            for (var k = 0; k < PartialRatios.Length; k++)
            {
                var f = hz * PartialRatios[k];
                if (f * 1.1f >= rate / 2f)
                {
                    continue;
                }
                var decay = RingDecay / (1f + (0.25f * k));
                var p0 = rng.NextUnit() * 2f * (float)Math.PI;
                var p1 = rng.NextUnit() * 2f * (float)Math.PI;
                var p2 = rng.NextUnit() * 2f * (float)Math.PI;
                var p3 = rng.NextUnit() * 2f * (float)Math.PI;
                for (var i = 0; i < n; i++)
                {
                    var t = (float)i / rate;
                    var w = 2.0 * Math.PI * t;
                    var s = Math.Sin((w * f) + p0)
                        + (0.5 * Math.Sin((w * (f + 3f)) + p1))
                        + (0.25 * Math.Sin((w * (f + 62f)) + p2))
                        + (0.2 * Math.Sin((w * (f - 62f)) + p3));
                    x[i] += (float)(s * PartialLevels[k] * RingEnvelope(t, decay));
                }
            }
            return x;
        }

        private static float RingEnvelope(float t, float decay)
        {
            if (t < RingPeakAt)
            {
                var u = t / RingPeakAt;
                var smooth = u * u * (3f - (2f * u));
                return 0.15f + (0.85f * (float)Math.Pow(smooth, 1.5));
            }
            return (float)Math.Exp(-(t - RingPeakAt) / decay);
        }

        private static float RumbleEnvelope(float t, float decay)
        {
            if (t < 0.03f)
            {
                return t / 0.03f;
            }
            return t < RumbleHoldUntil ? 1f : (float)Math.Exp(-(t - RumbleHoldUntil) / decay);
        }

        private static float ThunderEnvelope(float t, ThunderStyle style)
        {
            var roar = style == ThunderStyle.Roar;
            var attack = roar ? 0.06f : 0.04f;
            var hold = roar ? 0.4f : 0.3f;
            if (t < attack)
            {
                var u = t / attack;
                return roar ? u * u : u;
            }
            if (t < hold)
            {
                return 1f;
            }
            var rolled = ThunderFloor + ((1f - ThunderFloor) * (float)Math.Exp(-(t - hold) / ThunderDecay));
            return rolled * BodyCutoff(t);
        }

        /// <summary>
        /// The noisy layers stop shortly after the body holds (Dark Messiah's mid and high bands fall by 4–5× between 1.1
        /// and 1.3 s), so only the low rumble's tail and the ring are left.
        /// </summary>
        private static float BodyCutoff(float t) => t < RumbleHoldUntil ? 1f : (float)Math.Exp(-(t - RumbleHoldUntil) / 0.09f);

        /// <summary>Swells in; with a floor it stays at that share until the body ends (Dark Messiah's 2 kHz+ band stays at ~40 % to 1.1 s).</summary>
        private static float HissEnvelope(float t, float floor)
        {
            if (t < HissPeakAt)
            {
                return t / HissPeakAt;
            }
            var decayed = (float)Math.Exp(-(t - HissPeakAt) / HissDecay);
            return floor <= 0f ? decayed : (floor + ((1f - floor) * decayed)) * BodyCutoff(t);
        }

        /// <summary>A short fade at both ends so the clip never clicks; the first and last samples are 0.</summary>
        private static float Edges(int i, int n, int rate)
        {
            var g = 1f;
            var fadeIn = Math.Max(1f, FadeIn * rate);
            if (i < fadeIn)
            {
                g = i / fadeIn;
            }
            var left = n - 1 - i;
            var fadeOut = Math.Max(1f, FadeOut * rate);
            if (left < fadeOut)
            {
                g *= 0.5f * (1f - (float)Math.Cos(Math.PI * left / fadeOut));
            }
            return g;
        }

        /// <summary>
        /// Rounds off peaks (tanh), so the sound is about as loud as Dark Messiah's at the same peak (its crest factor
        /// is about 2.5).
        /// </summary>
        private static void SoftLimit(float[] x, float drive)
        {
            var norm = (float)Math.Tanh(drive);
            for (var i = 0; i < x.Length; i++)
            {
                x[i] = (float)Math.Tanh(drive * x[i]) / norm;
            }
        }

        private static void Normalise(float[] x, float peak)
        {
            var max = 0f;
            foreach (var v in x)
            {
                max = Math.Max(max, Math.Abs(v));
            }
            if (max <= 0f)
            {
                return;
            }
            var g = peak / max;
            for (var i = 0; i < x.Length; i++)
            {
                x[i] *= g;
            }
        }

        private sealed class OnePole
        {
            private readonly float _a;
            private float _y;

            public OnePole(float cutoffHz, int rate)
            {
                _a = 1f - (float)Math.Exp(-2.0 * Math.PI * cutoffHz / rate);
            }

            public float Next(float x)
            {
                _y += _a * (x - _y);
                return _y;
            }
        }
    }

    /// <summary>A small deterministic random source (xorshift32), the same on every runtime.</summary>
    internal sealed class XorShift
    {
        private uint _s;

        public XorShift(int seed)
        {
            _s = (uint)seed * 2654435761u;
            if (_s == 0)
            {
                _s = 0x9E3779B9u;
            }
        }

        public uint NextUInt()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        /// <summary>0 (inclusive) .. 1 (exclusive).</summary>
        public float NextUnit() => (NextUInt() >> 8) / 16777216f;

        /// <summary>-1 .. 1.</summary>
        public float NextSigned() => (NextUnit() * 2f) - 1f;
    }
}
