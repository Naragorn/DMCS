using System;
using System.Linq;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>Step 4d: the "adrenaline ready" sound (own synthesis after Dark Messiah's Adrenaline_Reached).</summary>
public class ReadySoundTests
{
    private const int Rate = 22050;

    private static float[] Sound(int seed = 1) => ReadySound.Generate(Rate, seed, new ReadySoundSpec());

    private static float[] Channel(float[] stereo, int ch) => Enumerable.Range(0, stereo.Length / 2).Select(i => stereo[(2 * i) + ch]).ToArray();

    private static float[] Mono(float[] stereo) => Enumerable.Range(0, stereo.Length / 2).Select(i => (stereo[2 * i] + stereo[(2 * i) + 1]) * 0.5f).ToArray();

    private static float[] Slice(float[] mono, double from, double to) =>
        mono.Skip((int)(from * Rate)).Take((int)((to - from) * Rate)).ToArray();

    /// <summary>
    /// Goertzel power at one frequency over a Hann-windowed slice, normalised by length. Without the window the strong
    /// rumble leaks into every other frequency and drowns what is measured there.
    /// </summary>
    private static double Power(float[] x, double hz)
    {
        var w = 2 * Math.PI * hz / Rate;
        var c = 2 * Math.Cos(w);
        double s1 = 0, s2 = 0;
        for (var i = 0; i < x.Length; i++)
        {
            var v = x[i] * (0.5 - (0.5 * Math.Cos(2 * Math.PI * i / Math.Max(1, x.Length - 1))));
            var s0 = v + (c * s1) - s2;
            s2 = s1;
            s1 = s0;
        }
        return ((s1 * s1) + (s2 * s2) - (c * s1 * s2)) / x.Length / x.Length;
    }

    private static double Rms(float[] x) => Math.Sqrt(x.Average(v => (double)v * v));

    [Fact]
    public void IsStereo_AndAsLongAsTheSpec()
    {
        var s = Sound();

        Assert.Equal(2 * (int)Math.Round(new ReadySoundSpec().Duration * Rate), s.Length);
    }

    [Fact]
    public void IsNormalised_BelowClipping()
    {
        var peak = Sound().Max(Math.Abs);

        Assert.InRange(peak, 0.85f, 0.9001f);
    }

    [Fact]
    public void SameSeed_SameSound_OtherSeed_OtherNoise()
    {
        Assert.Equal(Sound(3), Sound(3));
        Assert.NotEqual(Sound(3), Sound(4));
    }

    [Fact]
    public void Channels_AreNotTheSame_ForAWideSound()
    {
        var s = Sound();
        var l = Channel(s, 0);
        var r = Channel(s, 1);
        var ml = l.Average();
        var mr = r.Average();
        var cov = l.Zip(r, (a, b) => (a - ml) * (b - mr)).Sum();
        var corr = cov / Math.Sqrt(l.Sum(a => (a - ml) * (a - ml)) * r.Sum(b => (b - mr) * (b - mr)));

        Assert.InRange(corr, -0.2, 0.8);
    }

    [Fact]
    public void RingTone_StandsOutInTheTail()
    {
        var ringHz = new ReadySoundSpec().RingHz;
        var tail = Slice(Mono(Sound()), 1.3, 2.0);

        Assert.True(Power(tail, ringHz) > 50 * Power(tail, ringHz * 1.37), "ring vs off-pitch");
        Assert.True(Power(tail, ringHz * 2) > 20 * Power(tail, ringHz * 2.37), "second partial");
    }

    [Fact]
    public void RingTone_SwellsIn_ThenRingsOut()
    {
        var hz = new ReadySoundSpec().RingHz;
        var m = Mono(Sound());

        var early = Power(Slice(m, 0.0, 0.2), hz);
        var peak = Power(Slice(m, 0.8, 1.1), hz);
        var end = Power(Slice(m, 2.0, 2.2), hz);

        Assert.True(peak > 2 * early, "swells in");
        Assert.True(peak > 5 * end, "rings out");
        Assert.True(end > 0, "still rings at the end");
    }

    /// <summary>
    /// The thunder at the start (Dark Messiah's file, 100 ms bands: 300–2000 Hz at 98–111 in 0.1–0.3 s, about 25 from
    /// 0.6 s on): a broadband crack and roar in the mids that the rumble and hiss alone do not have.
    /// </summary>
    [Fact]
    public void Thunder_CracksInTheMids_AtTheStart_ThenRollsOff()
    {
        var m = Mono(Sound());

        // Band level as the analyser measured it: root of the summed power, sampled every 10 Hz.
        double Band(double from, double to, double lo, double hi)
        {
            var x = Slice(m, from, to);
            double sum = 0;
            for (var hz = lo; hz < hi; hz += 10)
            {
                sum += Power(x, hz);
            }
            return Math.Sqrt(sum);
        }

        var earlyMid = Band(0.05, 0.35, 300, 2000);
        var earlyLow = Band(0.05, 0.35, 20, 80);
        var lateMid = Band(0.7, 1.0, 300, 2000);

        Assert.True(earlyMid > 0.4 * earlyLow, "the crack roars in the mids: " + (earlyMid / earlyLow).ToString("0.00"));
        Assert.True(earlyMid > 2.5 * lateMid, "then rolls off: " + (earlyMid / lateMid).ToString("0.00"));
    }

    [Fact]
    public void Hiss_IsAnEarlyWhoosh()
    {
        var m = Mono(Sound());

        Assert.True(Power(Slice(m, 0.05, 0.3), 6000) > 20 * Power(Slice(m, 1.3, 1.6), 6000));
    }

    [Fact]
    public void Rumble_CarriesTheBody_ThenFades()
    {
        var m = Mono(Sound());

        Assert.True(Power(Slice(m, 0.1, 1.1), 60) > 20 * Power(Slice(m, 1.9, 2.2), 60));
        Assert.True(Rms(Slice(m, 0.1, 1.1)) > 3 * Rms(Slice(m, 1.6, 2.2)), "the body is loud, the tail quiet");
    }

    [Fact]
    public void EndsInSilence_SoNoClick()
    {
        var s = Sound();

        Assert.True(Math.Abs(s[^1]) < 1e-4 && Math.Abs(s[^2]) < 1e-4);
        Assert.True(Math.Abs(s[0]) < 0.05 && Math.Abs(s[1]) < 0.05);
    }

    [Fact]
    public void NoNaN()
    {
        Assert.DoesNotContain(Sound(), v => float.IsNaN(v) || float.IsInfinity(v));
    }
}

/// <summary>Step 4d: the Dark Messiah-style vignette frames (own drawing: red and orange streaks from the edges).</summary>
public class VignetteFlipbookTests
{
    private static VignetteSpec Spec(int streaks = 70, float length = 0.4f, float hue = 6f, int size = 64, int frames = 6) =>
        new VignetteSpec(size, frames, streaks, length, hue);

    private static float Brightness(byte[] rgba, int x, int y, int size)
    {
        var i = ((y * size) + x) * 4;
        return Math.Max(rgba[i], Math.Max(rgba[i + 1], rgba[i + 2])) / 255f;
    }

    /// <summary>Share of pixels brighter than a threshold within a ring of normalised radius [from, to).</summary>
    private static double LitShare(byte[] rgba, int size, double from, double to)
    {
        int lit = 0, all = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var nx = ((x + 0.5) / size * 2) - 1;
                var ny = ((y + 0.5) / size * 2) - 1;
                var r = Math.Sqrt((nx * nx) + (ny * ny));
                if (r < from || r >= to)
                {
                    continue;
                }
                all++;
                if (Brightness(rgba, x, y, size) > 0.15f)
                {
                    lit++;
                }
            }
        }
        return all == 0 ? 0 : (double)lit / all;
    }

    [Fact]
    public void Frames_HaveTheRequestedCountAndSize()
    {
        var frames = VignetteFlipbook.Generate(Spec(frames: 5, size: 32), seed: 1);

        Assert.Equal(5, frames.Length);
        Assert.All(frames, f => Assert.Equal(32 * 32 * 4, f.Length));
    }

    [Fact]
    public void Centre_StaysBlack_SoTheViewIsFree()
    {
        var frames = VignetteFlipbook.Generate(Spec(length: 0.5f), seed: 2);

        Assert.All(frames, f => Assert.True(LitShare(f, 64, 0, 0.4) == 0, "centre lit"));
    }

    [Fact]
    public void Edges_CarryStreaks()
    {
        var frames = VignetteFlipbook.Generate(Spec(), seed: 3);

        Assert.All(frames, f => Assert.True(LitShare(f, 64, 0.85, 1.0) > 0.05, "edge dark"));
    }

    [Fact]
    public void Streaks_SitElsewhereInEveryFrame_ThatIsTheFlicker()
    {
        var frames = VignetteFlipbook.Generate(Spec(), seed: 4);

        for (var i = 1; i < frames.Length; i++)
        {
            Assert.NotEqual(frames[i - 1], frames[i]);
        }
    }

    [Fact]
    public void SameSeed_SameFrames()
    {
        Assert.Equal(VignetteFlipbook.Generate(Spec(), 9), VignetteFlipbook.Generate(Spec(), 9));
    }

    [Fact]
    public void MoreStreaks_LightMoreOfTheEdge()
    {
        var few = VignetteFlipbook.Generate(Spec(streaks: 4), 5).Average(f => LitShare(f, 64, 0.7, 1.0));
        var many = VignetteFlipbook.Generate(Spec(streaks: 30), 5).Average(f => LitShare(f, 64, 0.7, 1.0));

        Assert.True(many > few * 1.5, few + " vs " + many);
    }

    [Fact]
    public void LongerStreaks_ReachFurtherIn()
    {
        var shortOnes = VignetteFlipbook.Generate(Spec(length: 0.15f), 6).Average(f => LitShare(f, 64, 0.55, 0.8));
        var longOnes = VignetteFlipbook.Generate(Spec(length: 0.6f), 6).Average(f => LitShare(f, 64, 0.55, 0.8));

        Assert.True(longOnes > shortOnes + 0.02, shortOnes + " vs " + longOnes);
    }

    [Fact]
    public void Colour_IsRedToOrange_NeverBlue()
    {
        var f = VignetteFlipbook.Generate(Spec(hue: 12f), 7)[0];

        for (var i = 0; i < f.Length; i += 4)
        {
            Assert.True(f[i] >= f[i + 1] && f[i + 1] >= f[i + 2], "pixel " + (i / 4));
        }
    }

    [Fact]
    public void Hue_ShiftsTowardsOrange()
    {
        double GreenShare(float hue)
        {
            var f = VignetteFlipbook.Generate(Spec(hue: hue), 8)[0];
            double r = 0, g = 0;
            for (var i = 0; i < f.Length; i += 4)
            {
                r += f[i];
                g += f[i + 1];
            }
            return g / Math.Max(1, r);
        }

        Assert.True(GreenShare(35f) > GreenShare(0f) + 0.1);
    }

    [Fact]
    public void Alpha_IsOpaque_AdditiveBlackIsSeeThrough()
    {
        var f = VignetteFlipbook.Generate(Spec(), 1)[0];

        for (var i = 3; i < f.Length; i += 4)
        {
            Assert.Equal(255, f[i]);
        }
    }

    [Theory]
    [InlineData(0, 5, 2, 0.2f)]
    [InlineData(5, 0, 2, 0.2f)]
    [InlineData(5, 2, 0, 0.2f)]
    [InlineData(5, 2, 2, 0f)]
    public void BadSpec_IsRefused(int size, int frames, int streaks, float length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VignetteFlipbook.Generate(new VignetteSpec(size, frames, streaks, length, 10f), 1));
    }

    [Theory]
    [InlineData(0f, 25f, 25, 0)]
    [InlineData(0.03f, 25f, 25, 0)]
    [InlineData(0.05f, 25f, 25, 1)]
    [InlineData(1.01f, 25f, 25, 0)]
    [InlineData(1.05f, 10f, 10, 0)]
    [InlineData(0.15f, 10f, 10, 1)]
    public void FrameAt_LoopsAtTheFrameRate(float seconds, float fps, int count, int expected)
    {
        Assert.Equal(expected, VignetteFlipbook.FrameAt(seconds, fps, count));
    }

    [Fact]
    public void FrameAt_ZeroRate_HoldsTheFirstFrame()
    {
        Assert.Equal(0, VignetteFlipbook.FrameAt(3f, 0f, 25));
    }
}

/// <summary>Step 4d: how strong the signals are from moment to moment.</summary>
public class SignalLevelTests
{
    [Fact]
    public void Fader_RisesOverFadeIn_AndFallsOverFadeOut()
    {
        var f = new SignalFader(fadeIn: 0.5f, fadeOut: 1f);

        Assert.Equal(0f, f.Level);
        f.Update(true, 0.25f);
        Assert.Equal(0.5f, f.Level, 3);
        f.Update(true, 1f);
        Assert.Equal(1f, f.Level);
        f.Update(false, 0.5f);
        Assert.Equal(0.5f, f.Level, 3);
        f.Update(false, 5f);
        Assert.Equal(0f, f.Level);
    }

    [Fact]
    public void Fader_ZeroTimes_SwitchAtOnce()
    {
        var f = new SignalFader(0f, 0f);

        f.Update(true, 0.01f);
        Assert.Equal(1f, f.Level);
        f.Update(false, 0.01f);
        Assert.Equal(0f, f.Level);
    }

    [Fact]
    public void Fader_Reset_DropsToZero()
    {
        var f = new SignalFader(0f, 1f);
        f.Update(true, 1f);

        f.Reset();

        Assert.Equal(0f, f.Level);
    }

    [Fact]
    public void Flare_PeaksAtOnce_ThenDecaysToZero()
    {
        var fl = new SignalFlare(duration: 0.6f);

        Assert.Equal(0f, fl.Level(0f));
        fl.Trigger(10f);
        Assert.Equal(1f, fl.Level(10f));
        Assert.InRange(fl.Level(10.3f), 0.01f, 0.99f);
        Assert.True(fl.Level(10.1f) > fl.Level(10.3f));
        Assert.Equal(0f, fl.Level(10.6f));
        Assert.Equal(0f, fl.Level(11f));
    }

    [Fact]
    public void Flare_Reset_EndsIt()
    {
        var fl = new SignalFlare(0.6f);
        fl.Trigger(0f);

        fl.Reset();

        Assert.Equal(0f, fl.Level(0.1f));
    }

    [Fact]
    public void Heartbeat_LubDub_ThenRest()
    {
        const float bpm = 60f;

        var mid = HeartbeatPulse.BeatLength / 2f;
        var lub = HeartbeatPulse.At(mid, bpm);
        var dub = HeartbeatPulse.At(HeartbeatPulse.DubDelay + mid, bpm);
        var rest = HeartbeatPulse.At(0.7f, bpm);

        Assert.True(lub > 0.8f);
        Assert.InRange(dub, 0.3f, 0.8f);
        Assert.Equal(0f, rest);
        Assert.Equal(lub, HeartbeatPulse.At(1f + mid, bpm), 3);
    }

    [Fact]
    public void Heartbeat_ZeroRate_IsQuiet()
    {
        Assert.Equal(0f, HeartbeatPulse.At(0.02f, 0f));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    public void SignalsRun_WhileAWindowIsOpen_AndTheFeatureIsOn(bool adrenalineWindow, bool executionWindow, bool featureOff, bool expected)
    {
        Assert.Equal(expected, AdrenalineSignalRule.Active(!featureOff, adrenalineWindow, executionWindow));
    }
}

public class AdrenalineSignalSettingsTests
{
    [Fact]
    public void Sliders_HaveTheirDefaultsOnTheSlider()
    {
        foreach (var s in AdrenalineSettings.SignalSliders)
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name);
        }
    }

    [Fact]
    public void Defaults_AsDarkMessiahAndTheSpec()
    {
        Assert.True(AdrenalineSettings.DefaultReadySound);
        Assert.True(AdrenalineSettings.DefaultHeartbeat);
        Assert.True(AdrenalineSettings.DefaultHaptics);
        Assert.True(AdrenalineSettings.DefaultWeaponGlow);
        Assert.True(AdrenalineSettings.DefaultVignette);
        Assert.True(AdrenalineSettings.DefaultVignetteFlare);
        Assert.Equal(0.6f, AdrenalineSettings.ReadySoundVolume.Default);
        Assert.Equal(0.5f, AdrenalineSettings.HeartbeatVolume.Default);
        Assert.Equal(25f, AdrenalineSettings.VignetteFrameRate.Default);
        Assert.Contains(AdrenalineSettings.DefaultVignetteStreaks, AdrenalineSettings.VignetteStreakValues());
    }
}

/// <summary>Writes the sound (WAV) and the vignette frames (PPM) to DMCS_DUMP_DIR for listening and looking; no-op without it.</summary>
public class SignalDumps
{
    [Fact]
    public void Dump()
    {
        var dir = Environment.GetEnvironmentVariable("DMCS_DUMP_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        System.IO.Directory.CreateDirectory(dir);
        const int rate = 22050;
        WriteWav(System.IO.Path.Combine(dir, "ready.wav"), ReadySound.Generate(rate, 1, new ReadySoundSpec()), rate);
        WriteWav(System.IO.Path.Combine(dir, "ready_v1.wav"), ReadySound.Generate(rate, 1, ReadySoundSpec.Version1()), rate);
        WriteWav(System.IO.Path.Combine(dir, "ready_v2.wav"), ReadySound.Generate(rate, 1, ReadySoundSpec.Version2()), rate);
        WriteWav(System.IO.Path.Combine(dir, "ready_v3.wav"), ReadySound.Generate(rate, 1, ReadySoundSpec.Version3()), rate);
        DumpFrames(dir);
    }

    private static void WriteWav(string path, float[] s, int rate)
    {
        using (var w = new System.IO.BinaryWriter(System.IO.File.Create(path)))
        {
            w.Write("RIFF".ToCharArray());
            w.Write(36 + (s.Length * 2));
            w.Write("WAVEfmt ".ToCharArray());
            w.Write(16);
            w.Write((short)1);
            w.Write((short)2);
            w.Write(rate);
            w.Write(rate * 4);
            w.Write((short)4);
            w.Write((short)16);
            w.Write("data".ToCharArray());
            w.Write(s.Length * 2);
            foreach (var v in s)
            {
                w.Write((short)Math.Round(Math.Clamp(v, -1f, 1f) * 32767));
            }
        }
    }

    private static void DumpFrames(string dir)
    {
        foreach (var (name, prefix) in new[] { (AdrenalineStyles.Dmcs1, "vig1_"), (AdrenalineStyles.Dmcs2, "vig2_"), (AdrenalineStyles.Dmcs3, "vig"), (AdrenalineStyles.Dmcs4, "vig4_") })
        {
            var (streaks, length, hue) = AdrenalineStyles.VignetteLook(name);
            var frames = VignetteFlipbook.Generate(new VignetteSpec(256, 25, streaks, length, hue, AdrenalineStyles.Vignette(name)), 1);
            for (var f = 0; f < frames.Length; f++)
            {
                WritePpm(System.IO.Path.Combine(dir, $"{prefix}{f:00}.ppm"), frames[f], 256);
            }
        }
    }

    internal static void WritePpm(string path, byte[] rgba, int size)
    {
        using var o = System.IO.File.Create(path);
        var head = System.Text.Encoding.ASCII.GetBytes($"P6 {size} {size} 255\n");
        o.Write(head, 0, head.Length);
        for (var i = 0; i < rgba.Length; i += 4)
        {
            o.WriteByte(rgba[i]);
            o.WriteByte(rgba[i + 1]);
            o.WriteByte(rgba[i + 2]);
        }
    }
}

/// <summary>Every built version can be chosen: the sound and vignette versions and their looks.</summary>
public class AdrenalineStyleTests
{
    private const int Rate = 22050;

    [Fact]
    public void Names_AndDefaults()
    {
        Assert.Equal(new[] { "DMCS 1", "DMCS 2", "DMCS 3", "Dark Messiah" }, AdrenalineStyles.SoundNames);
        Assert.Equal(new[] { "DMCS 1", "DMCS 2", "DMCS 3", "DMCS 4", "Dark Messiah" }, AdrenalineStyles.VignetteNames);
        Assert.Equal(new[] { "Blade & Sorcery", "Dark Messiah" }, AdrenalineStyles.HeartbeatNames);
        Assert.Equal("DMCS 3", AdrenalineStyles.DefaultSound);
        Assert.Equal("DMCS 3", AdrenalineStyles.DefaultVignette);
        Assert.Equal("Blade & Sorcery", AdrenalineStyles.DefaultHeartbeat);
        Assert.True(AdrenalineStyles.IsDarkMessiah("Dark Messiah"));
        Assert.False(AdrenalineStyles.IsDarkMessiah("DMCS 3"));
    }

    [Theory]
    [InlineData("DMCS 1", ThunderStyle.None)]
    [InlineData("DMCS 2", ThunderStyle.Crackle)]
    [InlineData("DMCS 3", ThunderStyle.Roar)]
    [InlineData("Dark Messiah", ThunderStyle.Roar)]
    [InlineData("nonsense", ThunderStyle.Roar)]
    public void SoundVersion_PicksItsSynthesis(string name, ThunderStyle thunder)
    {
        Assert.Equal(thunder, AdrenalineStyles.Sound(name).Thunder);
    }

    [Theory]
    [InlineData("DMCS 1", VignetteStyle.Flames)]
    [InlineData("DMCS 2", VignetteStyle.Streaks)]
    [InlineData("DMCS 3", VignetteStyle.Patchy)]
    [InlineData("DMCS 4", VignetteStyle.Embers)]
    [InlineData("Dark Messiah", VignetteStyle.Patchy)]
    public void VignetteVersion_PicksItsDrawing(string name, VignetteStyle style)
    {
        Assert.Equal(style, AdrenalineStyles.Vignette(name));
    }

    [Theory]
    [InlineData("DMCS 1", 14, 0.35f, 12f)]
    [InlineData("DMCS 2", 40, 0.45f, 6f)]
    [InlineData("DMCS 3", 70, 0.4f, 6f)]
    [InlineData("DMCS 4", 90, 0.5f, 10f)]
    public void VignetteVersion_LooksAsBuilt_AndFitsTheSliders(string name, int streaks, float length, float hue)
    {
        var look = AdrenalineStyles.VignetteLook(name);

        Assert.Equal((streaks, length, hue), look);
        Assert.Contains(look.streaks, AdrenalineSettings.VignetteStreakValues());
        Assert.True(SliderValues.IndexOf(AdrenalineSettings.VignetteLength.Values(), look.length) >= 0);
        Assert.True(SliderValues.IndexOf(AdrenalineSettings.VignetteHue.Values(), look.hue) >= 0);
    }

    [Theory]
    [InlineData(VignetteStyle.Flames)]
    [InlineData(VignetteStyle.Streaks)]
    [InlineData(VignetteStyle.Patchy)]
    [InlineData(VignetteStyle.Embers)]
    public void EveryDrawing_KeepsTheCentreFree_LightsTheEdge_IsRedOrange(VignetteStyle style)
    {
        var frames = style == VignetteStyle.Embers
            ? VignetteFlipbook.Generate(new VignetteSpec(64, 4, AdrenalineStyles.Dmcs4Streaks, AdrenalineStyles.Dmcs4Length, AdrenalineStyles.Dmcs4Hue, style), 2)
            : VignetteFlipbook.Generate(new VignetteSpec(64, 4, 30, 0.4f, 6f, style), 2);

        foreach (var f in frames)
        {
            int centreLit = 0, edgeLit = 0, edge = 0;
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var nx = ((x + 0.5) / 64 * 2) - 1;
                    var ny = ((y + 0.5) / 64 * 2) - 1;
                    var r = Math.Sqrt((nx * nx) + (ny * ny));
                    var i = ((y * 64) + x) * 4;
                    var lit = f[i] > 38;
                    Assert.True(f[i] >= f[i + 1] && f[i + 1] >= f[i + 2]);
                    if (r < 0.4 && lit)
                    {
                        centreLit++;
                    }
                    if (r >= 0.85 && r < 1)
                    {
                        edge++;
                        edgeLit += lit ? 1 : 0;
                    }
                }
            }
            Assert.Equal(0, centreLit);
            // DMCS 4 is as sparse as Dark Messiah along the edge (its light sits in the corners): 1 % is enough there.
            Assert.True(edgeLit > edge / (style == VignetteStyle.Embers ? 100 : 50), style + ": edge " + edgeLit + "/" + edge);
        }
    }

    private static double Correlation(byte[] a, byte[] b)
    {
        double ma = 0, mb = 0;
        var n = a.Length / 4;
        for (var i = 0; i < a.Length; i += 4)
        {
            ma += a[i];
            mb += b[i];
        }
        ma /= n;
        mb /= n;
        double cov = 0, va = 0, vb = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            cov += (a[i] - ma) * (b[i] - mb);
            va += (a[i] - ma) * (a[i] - ma);
            vb += (b[i] - mb) * (b[i] - mb);
        }
        return cov / Math.Sqrt(va * vb);
    }

    /// <summary>
    /// DMCS 4 follows Dark Messiah's measured frames (2026-11-17): neighbouring frames correlate 0.89 there, five apart
    /// 0.59, the loop has no seam; 4 % of the pixels are lit. DMCS 1–3 draw every frame anew.
    /// </summary>
    [Fact]
    public void Embers_ShimmerInPlace_LikeDarkMessiah_TheOthersFlicker()
    {
        var (streaks, length, hue) = AdrenalineStyles.VignetteLook(AdrenalineStyles.Dmcs4);
        var e = VignetteFlipbook.Generate(new VignetteSpec(128, 25, streaks, length, hue, VignetteStyle.Embers), 1);
        var p = VignetteFlipbook.Generate(new VignetteSpec(128, 25, 70, 0.4f, 6f, VignetteStyle.Patchy), 1);

        var next = Enumerable.Range(0, 25).Average(f => Correlation(e[f], e[(f + 1) % 25]));
        var five = Enumerable.Range(0, 25).Average(f => Correlation(e[f], e[(f + 5) % 25]));
        var lit = e.Average(f => Enumerable.Range(0, f.Length / 4).Count(i => f[i * 4] > 38) / (double)(f.Length / 4));

        Assert.InRange(next, 0.85, 0.98);
        Assert.InRange(five, 0.45, 0.75);
        Assert.True(Correlation(e[24], e[0]) > 0.85, "seamless loop");
        Assert.InRange(lit, 0.025, 0.06);
        Assert.True(Correlation(p[0], p[1]) < 0.3, "DMCS 3 flickers");
    }

    [Fact]
    public void TheDrawings_DifferFromEachOther()
    {
        byte[] First(VignetteStyle s) => VignetteFlipbook.Generate(new VignetteSpec(64, 1, 30, 0.4f, 6f, s), 3)[0];

        Assert.NotEqual(First(VignetteStyle.Flames), First(VignetteStyle.Streaks));
        Assert.NotEqual(First(VignetteStyle.Streaks), First(VignetteStyle.Patchy));
    }

    private static float[] Mono(float[] stereo) => Enumerable.Range(0, stereo.Length / 2).Select(i => (stereo[2 * i] + stereo[(2 * i) + 1]) * 0.5f).ToArray();

    /// <summary>Band level (Hann-windowed Goertzel every 10 Hz), as the sound tests measure.</summary>
    private static double Band(float[] m, double from, double to, double lo, double hi)
    {
        var x = m.Skip((int)(from * Rate)).Take((int)((to - from) * Rate)).ToArray();
        double sum = 0;
        for (var hz = lo; hz < hi; hz += 10)
        {
            var w = 2 * Math.PI * hz / Rate;
            var c = 2 * Math.Cos(w);
            double s1 = 0, s2 = 0;
            for (var i = 0; i < x.Length; i++)
            {
                var v = x[i] * (0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (x.Length - 1))));
                var s0 = v + (c * s1) - s2;
                s2 = s1;
                s1 = s0;
            }
            sum += ((s1 * s1) + (s2 * s2) - (c * s1 * s2)) / x.Length / x.Length;
        }
        return Math.Sqrt(sum);
    }

    [Fact]
    public void Version1_HasNoThunder_Versions2And3_Have()
    {
        double MidToLow(ReadySoundSpec spec)
        {
            var m = Mono(ReadySound.Generate(Rate, 1, spec));
            return Band(m, 0.05, 0.35, 300, 2000) / Band(m, 0.05, 0.35, 20, 80);
        }

        Assert.True(MidToLow(ReadySoundSpec.Version1()) < 0.35, "v1");
        Assert.True(MidToLow(ReadySoundSpec.Version2()) > 0.4, "v2");
        Assert.True(MidToLow(ReadySoundSpec.Version3()) > 0.4, "v3");
    }

    [Fact]
    public void Version2Crackles_BrighterThanVersion3sRoar()
    {
        double Bright(ReadySoundSpec spec)
        {
            var m = Mono(ReadySound.Generate(Rate, 1, spec));
            return Band(m, 0.05, 0.35, 1200, 2000) / Band(m, 0.05, 0.35, 300, 700);
        }

        Assert.True(Bright(ReadySoundSpec.Version2()) > 1.3 * Bright(ReadySoundSpec.Version3()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryVersion_IsNormalisedStereo_WithoutClicks(int version)
    {
        var spec = version == 1 ? ReadySoundSpec.Version1() : version == 2 ? ReadySoundSpec.Version2() : ReadySoundSpec.Version3();

        var s = ReadySound.Generate(Rate, 1, spec);

        Assert.Equal(2 * (int)Math.Round(2.3 * Rate), s.Length);
        Assert.InRange(s.Max(Math.Abs), 0.85f, 0.9001f);
        Assert.True(Math.Abs(s[0]) < 0.05 && Math.Abs(s[^1]) < 1e-4);
    }
}
