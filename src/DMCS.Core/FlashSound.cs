using System;
using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    /// <summary>One tone of the ring: frequency, level and decay time constant (s).</summary>
    public readonly struct Partial
    {
        public Partial(float hz, float level, float decay)
        {
            Hz = hz;
            Level = level;
            Decay = decay;
        }

        public float Hz { get; }
        public float Level { get; }
        public float Decay { get; }
    }

    /// <summary>
    /// What a clash flash sound is made of: a low boom (sines plus low-passed noise under one envelope that holds, then
    /// decays in two stages; tones below 45 Hz decay slower), two band-passed noise layers for the body, a short
    /// band-passed noise strike and a metallic ring of decaying sines. Every DMCS version is built from these.
    /// </summary>
    public sealed class FlashSoundSpec
    {
        public float Duration { get; set; } = 3.6f;
        public float Peak { get; set; } = 0.86f;

        public float Attack { get; set; } = 0.008f;
        public float Hold { get; set; } = 0.95f;
        public float Decay1 { get; set; } = 0.6f;
        public float Knee { get; set; } = 2.0f;
        public float Decay2 { get; set; } = 0.45f;

        /// <summary>Decay of the tones below 45 Hz and the sub noise, as a multiple of the others.</summary>
        public float LowSlower { get; set; } = 1.5f;

        /// <summary>The boom's pitch sinks by this share over 2 s.</summary>
        public float PitchDrop { get; set; } = 0.05f;

        public Partial[] Subs { get; set; } = new Partial[0];
        public float SubsLevel { get; set; } = 0.7f;
        public float SubNoiseCutoff { get; set; } = 110f;
        public float SubNoiseLevel { get; set; }

        public float BodyHz { get; set; } = 300f;
        public float BodyLevel { get; set; }
        public float BodyDecay { get; set; } = 0.12f;
        public float BodyFloor { get; set; } = 0.25f;
        public float BodyBurst { get; set; }

        public float UpperHz { get; set; } = 900f;
        public float UpperLevel { get; set; }
        public float UpperDecay { get; set; } = 0.08f;
        public float UpperFloor { get; set; } = 0.05f;

        public float StrikeHz { get; set; } = 2200f;
        public float StrikeLevel { get; set; }
        public float StrikeDecay { get; set; } = 0.03f;

        public Partial[] Ring { get; set; } = new Partial[0];
        public float RingLevel { get; set; }

        /// <summary>
        /// DMCS 1: modelled on Sekiro's perilous-attack cue, measured 2026-11-17 in a recording (YouTube 5YzN2bPgjB0,
        /// "pitched and slowed to try and match its in-game appearance" by its uploader; Sekiro's own files are encrypted
        /// and were not read). 3.6 s; a sub boom (~31 Hz plus 60–115 Hz) loud to ~0.9 s, down to a fifth at 2 s; a broadband
        /// strike in the first 50 ms; a metallic ring at ~1863 Hz (beating with 1670/1790 Hz) with its octave 3670 Hz and
        /// short top partials 5460/7345 Hz. Matched per 0.25 s in seven bands (mean 2.2 dB off), the loudness curve
        /// (1.3 dB) and the ring partials (within ~3 dB in the first 0.6 s). Nothing of Sekiro's is shipped.
        /// </summary>
        public static FlashSoundSpec Version1() => new FlashSoundSpec
        {
            Subs = new[] { new Partial(31f, 0.6f, 0f), new Partial(63f, 0.2f, 0f), new Partial(84f, 0.4f, 0f), new Partial(97f, 0.2f, 0f), new Partial(113f, 0.12f, 0f) },
            SubNoiseLevel = 8f,
            BodyLevel = 2.2f,
            BodyBurst = 1.6f,
            UpperLevel = 0.9f,
            StrikeLevel = 2.6f,
            Ring = new[]
            {
                new Partial(1670f, 0.25f, 0.12f), new Partial(1790f, 0.25f, 0.15f), new Partial(1863f, 1.3f, 0.25f), new Partial(1863f, 0.3f, 0.7f),
                new Partial(3670f, 5f, 0.07f), new Partial(3670f, 0.15f, 0.5f), new Partial(5460f, 3f, 0.06f), new Partial(7345f, 3f, 0.06f),
            },
            RingLevel = 0.02f,
        };

        /// <summary>
        /// DMCS 2 (2026-11-20): DMCS 1 made short (1.6 s instead of 3.6 s) for fights where many swings flash: the same
        /// boom, strike and 1863 Hz ring, the boom loud to ~0.3 s and gone by 1.5 s.
        /// </summary>
        public static FlashSoundSpec Version2()
        {
            var s = Version1();
            s.Duration = 1.6f;
            s.Hold = 0.3f;
            s.Decay1 = 0.25f;
            s.Knee = 0.8f;
            s.Decay2 = 0.15f;
            s.Ring = new[]
            {
                new Partial(1670f, 0.25f, 0.12f), new Partial(1790f, 0.25f, 0.15f), new Partial(1863f, 1.3f, 0.2f), new Partial(1863f, 0.3f, 0.3f),
                new Partial(3670f, 5f, 0.07f), new Partial(3670f, 0.15f, 0.25f), new Partial(5460f, 3f, 0.06f), new Partial(7345f, 3f, 0.06f),
            };
            return s;
        }

        /// <summary>
        /// DMCS 3 (2026-11-20): darker and longer than DMCS 1, like a distant bell over thunder: a sub boom down to 24 Hz that
        /// holds ~1.2 s, a heavier body, and the ring an octave lower (932 Hz, beating with 885 Hz).
        /// </summary>
        public static FlashSoundSpec Version3() => new FlashSoundSpec
        {
            Duration = 4.5f,
            Hold = 1.2f,
            Decay1 = 0.8f,
            Knee = 2.6f,
            Decay2 = 0.5f,
            Subs = new[] { new Partial(24f, 0.7f, 0f), new Partial(48f, 0.3f, 0f), new Partial(62f, 0.35f, 0f), new Partial(75f, 0.2f, 0f) },
            SubNoiseCutoff = 80f,
            SubNoiseLevel = 9f,
            BodyHz = 220f,
            BodyLevel = 2f,
            BodyBurst = 1.2f,
            UpperHz = 700f,
            UpperLevel = 0.6f,
            StrikeHz = 1500f,
            StrikeLevel = 2.2f,
            Ring = new[]
            {
                new Partial(885f, 0.25f, 0.2f), new Partial(932f, 1.3f, 0.4f), new Partial(932f, 0.3f, 1.1f),
                new Partial(1840f, 2f, 0.12f), new Partial(2790f, 1f, 0.08f),
            },
            RingLevel = 0.03f,
        };

        /// <summary>
        /// DMCS 4 (2026-11-20): bright, so it cuts through the noise of a fight: the ring (2794 Hz with its octave and a
        /// high partial) carries the sound, over a light boom and a sharp high strike; 3 s.
        /// </summary>
        public static FlashSoundSpec Version4() => new FlashSoundSpec
        {
            Duration = 3f,
            Hold = 0.4f,
            Decay1 = 0.35f,
            Knee = 1.2f,
            Decay2 = 0.3f,
            Subs = new[] { new Partial(45f, 0.4f, 0f), new Partial(90f, 0.3f, 0f) },
            SubsLevel = 0.35f,
            SubNoiseLevel = 3f,
            BodyHz = 500f,
            BodyLevel = 1.2f,
            UpperHz = 1800f,
            UpperLevel = 1.2f,
            StrikeHz = 4500f,
            StrikeLevel = 3f,
            Ring = new[]
            {
                new Partial(2700f, 0.25f, 0.2f), new Partial(2794f, 1.3f, 0.35f), new Partial(2794f, 0.3f, 0.8f),
                new Partial(5588f, 3f, 0.08f), new Partial(8200f, 2f, 0.05f),
            },
            RingLevel = 0.3f,
        };
    }

    /// <summary>Synthesises the clash flash sound (interleaved stereo floats). Pure and deterministic per seed.</summary>
    public static class FlashSound
    {
        public const string Off = "Off";

        public static readonly string[] Styles = { Off, "DMCS 1", "DMCS 2", "DMCS 3", "DMCS 4" };

        public const string DefaultStyle = "DMCS 1";

        /// <summary>The spec of a style name; null for Off or an unknown name.</summary>
        public static FlashSoundSpec Spec(string style)
        {
            switch (style)
            {
                case "DMCS 1": return FlashSoundSpec.Version1();
                case "DMCS 2": return FlashSoundSpec.Version2();
                case "DMCS 3": return FlashSoundSpec.Version3();
                case "DMCS 4": return FlashSoundSpec.Version4();
                default: return null;
            }
        }

        public static float[] Generate(int sampleRate, int seed, FlashSoundSpec spec)
        {
            var n = (int)(spec.Duration * sampleRate);
            var rng = new Random(seed);
            var shared = new float[n];
            for (var i = 0; i < n; i++)
            {
                var t = (double)i / sampleRate;
                var boom = Envelope(spec, t, 1f);
                var low = Envelope(spec, t, spec.LowSlower);
                var sines = 0.0;
                var phaseT = t * (1 - (spec.PitchDrop * Math.Min(t, 2.0) / 2.0));
                foreach (var p in spec.Subs)
                {
                    sines += p.Level * Math.Sin(2 * Math.PI * p.Hz * phaseT) * (p.Hz < 45f ? low : boom);
                }
                var ring = 0.0;
                var ringAttack = Math.Min(1.0, t / 0.003);
                for (var k = 0; k < spec.Ring.Length; k++)
                {
                    var p = spec.Ring[k];
                    // Equal phases: two partials of one frequency (a fast and a slow decay) add up instead of cancelling.
                    ring += p.Level * Math.Sin(2 * Math.PI * p.Hz * t) * Math.Exp(-t / p.Decay) * ringAttack;
                }
                shared[i] = (float)((sines * spec.SubsLevel) + (ring * spec.RingLevel));
            }

            var output = new float[n * 2];
            for (var ch = 0; ch < 2; ch++)
            {
                var subA = Biquad.LowPass(sampleRate, spec.SubNoiseCutoff, 0.7);
                var subB = Biquad.LowPass(sampleRate, spec.SubNoiseCutoff, 0.7);
                var bodyA = Biquad.BandPass(sampleRate, spec.BodyHz, 0.8);
                var bodyB = Biquad.BandPass(sampleRate, spec.BodyHz, 0.8);
                var upperA = Biquad.BandPass(sampleRate, spec.UpperHz, 0.9);
                var upperB = Biquad.BandPass(sampleRate, spec.UpperHz, 0.9);
                var strike = Biquad.BandPass(sampleRate, spec.StrikeHz, 0.45);
                for (var i = 0; i < n; i++)
                {
                    var t = (double)i / sampleRate;
                    var boom = Envelope(spec, t, 1f);
                    var low = Envelope(spec, t, spec.LowSlower);
                    var noise = Gaussian(rng);
                    var sub = subB.Process(subA.Process(noise)) * spec.SubNoiseLevel * low;
                    var bodyEnv = (Math.Exp(-t / spec.BodyDecay) * (1 - spec.BodyFloor)) + spec.BodyFloor + (spec.BodyBurst * Math.Exp(-t / 0.05));
                    var body = bodyB.Process(bodyA.Process(noise)) * spec.BodyLevel * bodyEnv * boom;
                    var upperEnv = (Math.Exp(-t / spec.UpperDecay) * (1 - spec.UpperFloor)) + spec.UpperFloor;
                    var upper = upperB.Process(upperA.Process(noise)) * spec.UpperLevel * upperEnv * boom;
                    var hit = strike.Process(Gaussian(rng)) * spec.StrikeLevel * Math.Exp(-t / spec.StrikeDecay) * Math.Min(1.0, t / 0.002);
                    output[(2 * i) + ch] = (float)(shared[i] + sub + body + upper + hit);
                }
            }

            var peak = output.Max(Math.Abs);
            if (peak > 0f)
            {
                var g = spec.Peak / peak;
                for (var i = 0; i < output.Length; i++)
                {
                    output[i] *= g;
                }
            }
            return output;
        }

        /// <summary>Attack, hold, then two decay stages; <paramref name="slower"/> stretches both decays.</summary>
        private static double Envelope(FlashSoundSpec s, double t, float slower)
        {
            var attack = s.Attack > 0f ? Math.Min(1.0, t / s.Attack) : 1.0;
            var first = t < s.Hold ? 1.0 : Math.Exp(-(t - s.Hold) / (s.Decay1 * slower));
            var second = t < s.Knee ? 1.0 : Math.Exp(-(t - s.Knee) / (s.Decay2 * slower));
            return attack * first * second;
        }

        private static float Gaussian(Random rng)
        {
            var u1 = 1.0 - rng.NextDouble();
            var u2 = rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>All DMCS styles that make a sound (for building them ahead).</summary>
        public static IEnumerable<string> Sounding => Styles.Where(s => s != Off);
    }

    /// <summary>An RBJ biquad filter (Audio EQ Cookbook): low-pass, or band-pass with 0 dB peak gain.</summary>
    public sealed class Biquad
    {
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            _b0 = b0 / a0;
            _b1 = b1 / a0;
            _b2 = b2 / a0;
            _a1 = a1 / a0;
            _a2 = a2 / a0;
        }

        public static Biquad LowPass(int sampleRate, double hz, double q)
        {
            var w = 2 * Math.PI * hz / sampleRate;
            var c = Math.Cos(w);
            var alpha = Math.Sin(w) / (2 * q);
            return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + alpha, -2 * c, 1 - alpha);
        }

        public static Biquad BandPass(int sampleRate, double hz, double q)
        {
            var w = 2 * Math.PI * hz / sampleRate;
            var c = Math.Cos(w);
            var alpha = Math.Sin(w) / (2 * q);
            return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * c, 1 - alpha);
        }

        public float Process(float x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return (float)y;
        }
    }
}
