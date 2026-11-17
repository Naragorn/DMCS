using System;
using System.Linq;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class FlashSoundTests
{
    private const int Rate = 48000;

    private static float[] Mono(float[] stereo) => Enumerable.Range(0, stereo.Length / 2).Select(i => (stereo[2 * i] + stereo[(2 * i) + 1]) / 2f).ToArray();

    private static double Rms(float[] m, double from, double to)
    {
        var a = (int)(from * Rate);
        var b = Math.Min(m.Length, (int)(to * Rate));
        double sum = 0;
        for (var i = a; i < b; i++)
        {
            sum += m[i] * m[i];
        }
        return Math.Sqrt(sum / Math.Max(1, b - a));
    }

    /// <summary>Hann-windowed Goertzel magnitude of one frequency in a stretch.</summary>
    private static double Tone(float[] m, double from, double to, double hz)
    {
        var a = (int)(from * Rate);
        var n = Math.Min(m.Length, (int)(to * Rate)) - a;
        var w = 2 * Math.PI * hz / Rate;
        var c = 2 * Math.Cos(w);
        double s1 = 0, s2 = 0;
        for (var i = 0; i < n; i++)
        {
            var win = 0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (n - 1)));
            var s = (m[a + i] * win) + (c * s1) - s2;
            s2 = s1;
            s1 = s;
        }
        return Math.Sqrt((s1 * s1) + (s2 * s2) - (c * s1 * s2)) / n;
    }

    private static float[] Gen(FlashSoundSpec spec, int seed = 1) => FlashSound.Generate(Rate, seed, spec);

    [Fact]
    public void Stereo_OfTheSpecsLength_AndPeakNormalised()
    {
        var spec = FlashSoundSpec.Version1();
        var s = Gen(spec);

        Assert.Equal((int)(spec.Duration * Rate) * 2, s.Length);
        Assert.Equal(spec.Peak, s.Max(Math.Abs), 3);
    }

    [Fact]
    public void SameSeed_SameSound()
    {
        Assert.Equal(Gen(FlashSoundSpec.Version1(), 7), Gen(FlashSoundSpec.Version1(), 7));
    }

    [Fact]
    public void LeftAndRight_DifferSomewhat()
    {
        var s = Gen(FlashSoundSpec.Version1());
        var diff = Enumerable.Range(0, s.Length / 2).Max(i => Math.Abs(s[2 * i] - s[(2 * i) + 1]));

        Assert.True(diff > 0.05f);
    }

    [Fact]
    public void Version1_IsADeepBoom_ThatHoldsThenDecays()
    {
        // As measured in the Sekiro reference (2026-11-17): loud to ~0.9 s, down to about a fifth at 2 s, near silent at 3.3 s.
        var m = Mono(Gen(FlashSoundSpec.Version1()));
        var body = Rms(m, 0.1, 0.6);

        Assert.True(Rms(m, 0.6, 0.9) > 0.8 * body);
        Assert.InRange(Rms(m, 2.0, 2.25) / body, 0.1, 0.35);
        Assert.True(Rms(m, 3.25, 3.5) < 0.03 * body);
        Assert.True(Tone(m, 0.1, 0.6, 84) > 10 * Tone(m, 0.1, 0.6, 1200), "the low end carries it");
    }

    [Fact]
    public void Version1_RingsMetallic_At1863HzAndItsOctave()
    {
        var m = Mono(Gen(FlashSoundSpec.Version1()));

        Assert.True(Tone(m, 0.06, 0.4, 1863) > 4 * Tone(m, 0.06, 0.4, 1500), "ring above its neighbourhood");
        Assert.True(Tone(m, 0.06, 0.2, 3670) > 4 * Tone(m, 0.06, 0.2, 3300), "octave partial");
        Assert.True(Tone(m, 0.0, 0.06, 7345) > 3 * Tone(m, 0.4, 0.6, 7345), "top partials only in the strike");
        Assert.True(Tone(m, 1.2, 2.0, 1863) < Tone(m, 0.2, 0.6, 1863), "the ring dies out");
    }

    [Fact]
    public void Version1_StrikesWithAShortBrightNoise()
    {
        var m = Mono(Gen(FlashSoundSpec.Version1()));

        Assert.True(Tone(m, 0.0, 0.05, 2500) > 5 * Tone(m, 0.5, 0.55, 2500));
    }

    [Fact]
    public void Biquad_BandPass_PassesItsCentre_AndDampsFarAway()
    {
        double Gain(double hz)
        {
            var f = Biquad.BandPass(Rate, 1000, 0.8);
            double peak = 0;
            for (var i = 0; i < Rate / 2; i++)
            {
                var y = f.Process((float)Math.Sin(2 * Math.PI * hz * i / Rate));
                if (i > Rate / 4)
                {
                    peak = Math.Max(peak, Math.Abs(y));
                }
            }
            return peak;
        }

        Assert.InRange(Gain(1000), 0.95, 1.05);
        Assert.True(Gain(100) < 0.2);
        Assert.True(Gain(10000) < 0.2);
    }

    [Fact]
    public void Biquad_LowPass_KeepsLows_CutsHighs()
    {
        double Gain(double hz)
        {
            var f = Biquad.LowPass(Rate, 200, 0.7);
            double peak = 0;
            for (var i = 0; i < Rate / 2; i++)
            {
                var y = f.Process((float)Math.Sin(2 * Math.PI * hz * i / Rate));
                if (i > Rate / 4)
                {
                    peak = Math.Max(peak, Math.Abs(y));
                }
            }
            return peak;
        }

        Assert.InRange(Gain(40), 0.9, 1.1);
        Assert.True(Gain(4000) < 0.01);
    }

    [Fact]
    public void Styles_ListOffThenDmcs1()
    {
        Assert.Equal(new[] { "Off", "DMCS 1" }, FlashSound.Styles);
        Assert.Null(FlashSound.Spec("Off"));
        Assert.NotNull(FlashSound.Spec("DMCS 1"));
        Assert.Null(FlashSound.Spec("DMCS 2"));
        Assert.Null(FlashSound.Spec("whatever"));
    }
}

/// <summary>Writes every DMCS flash sound as a WAV file when DMCS_DUMP_DIR is set (for listening and for comparing against a reference outside the tests).</summary>
public class FlashSoundDump
{
    [Fact]
    public void WritesWavs_WhenAsked()
    {
        var dir = Environment.GetEnvironmentVariable("DMCS_DUMP_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        foreach (var style in FlashSound.Sounding)
        {
            var s = FlashSound.Generate(48000, 1, FlashSound.Spec(style)!);
            using var f = System.IO.File.Create(System.IO.Path.Combine(dir, "flash_" + style.Replace(" ", "") + ".wav"));
            using var w = new System.IO.BinaryWriter(f);
            var bytes = s.Length * 2;
            w.Write("RIFF"u8.ToArray()); w.Write(36 + bytes); w.Write("WAVEfmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)2);
            w.Write(48000); w.Write(48000 * 4); w.Write((short)4); w.Write((short)16); w.Write("data"u8.ToArray()); w.Write(bytes);
            foreach (var v in s)
            {
                w.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767));
            }
        }
    }
}
