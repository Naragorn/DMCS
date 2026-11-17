using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>Readers for Dark Messiah's files (VPK, WAV, VTF), with hand-built files; real files only when DMCS_DM_DIR is set.</summary>
public class DarkMessiahFilesTests
{
    /// <summary>A VPK v2 directory with the given files; data either in archive 0 (returned separately) or in the directory.</summary>
    private static (byte[] dir, byte[] archive) Vpk(IEnumerable<(string ext, string folder, string name, byte[] data, bool inDir, int preload)> files)
    {
        var tree = new MemoryStream();
        var dirData = new MemoryStream();
        var archive = new MemoryStream();
        void Str(string s)
        {
            var b = Encoding.ASCII.GetBytes(s);
            tree.Write(b, 0, b.Length);
            tree.WriteByte(0);
        }
        foreach (var byExt in files.GroupBy(f => f.ext))
        {
            Str(byExt.Key);
            foreach (var byFolder in byExt.GroupBy(f => f.folder))
            {
                Str(byFolder.Key);
                foreach (var f in byFolder)
                {
                    Str(f.name);
                    var w = new BinaryWriter(tree);
                    var pre = f.data.Take(f.preload).ToArray();
                    var rest = f.data.Skip(f.preload).ToArray();
                    var target = f.inDir ? dirData : archive;
                    w.Write(0u);
                    w.Write((ushort)pre.Length);
                    w.Write(f.inDir ? VpkDirectory.InDirectory : (ushort)0);
                    w.Write((uint)target.Length);
                    w.Write((uint)rest.Length);
                    w.Write((ushort)0xffff);
                    w.Write(pre);
                    target.Write(rest, 0, rest.Length);
                }
                Str(string.Empty);
            }
            Str(string.Empty);
        }
        Str(string.Empty);
        var head = new MemoryStream();
        var h = new BinaryWriter(head);
        h.Write(0x55aa1234u);
        h.Write(2u);
        h.Write((uint)tree.Length);
        h.Write((uint)dirData.Length);
        h.Write(0u);
        h.Write(0u);
        h.Write(0u);
        h.Write(tree.ToArray());
        h.Write(dirData.ToArray());
        return (head.ToArray(), archive.ToArray());
    }

    private static byte[] Bytes(int n, int seed) => Enumerable.Range(0, n).Select(i => (byte)((i * 31) + seed)).ToArray();

    [Fact]
    public void Vpk_FindsAndReads_ArchiveDirectoryAndPreloadData()
    {
        var a = Bytes(100, 1);
        var b = Bytes(50, 2);
        var c = Bytes(30, 3);
        var (dir, archive) = Vpk(new[]
        {
            ("wav", "mm/sound/x", "a", a, false, 0),
            ("wav", "mm/sound/x", "b", b, true, 0),
            ("vtf", "mm/materials", "c", c, false, 10),
        });
        var vpk = VpkDirectory.Parse(dir);
        byte[] ReadArchive(int index, long offset, int length) => archive.Skip((int)offset).Take(length).ToArray();

        Assert.Equal(3, vpk.Count);
        Assert.Equal(a, vpk.Read(vpk.Find("mm/sound/x/a.wav"), dir, ReadArchive));
        Assert.Equal(b, vpk.Read(vpk.Find("MM/Sound/X/B.WAV"), dir, ReadArchive));
        Assert.Equal(c, vpk.Read(vpk.Find("mm\\materials\\c.vtf"), dir, ReadArchive));
        Assert.Null(vpk.Find("mm/sound/x/missing.wav"));
    }

    [Fact]
    public void Vpk_RefusesOtherFiles()
    {
        Assert.Throws<InvalidDataException>(() => VpkDirectory.Parse(new byte[] { 1, 2, 3, 4, 2, 0, 0, 0, 0, 0, 0, 0 }));
    }

    [Fact]
    public void Vpk_ArchivePath_IsNumbered()
    {
        Assert.Equal("x/depot_2107_003.vpk", VpkDirectory.ArchivePath("x/depot_2107_dir.vpk", 3));
    }

    private static byte[] Wav(short[] samples, int channels, int rate, int format = 1, int bits = 16)
    {
        var m = new MemoryStream();
        var w = new BinaryWriter(m);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(0);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("LIST"));
        w.Write(3);
        w.Write(new byte[] { 1, 2, 3, 0 }); // odd chunk + pad byte
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((ushort)format);
        w.Write((ushort)channels);
        w.Write(rate);
        w.Write(rate * channels * 2);
        w.Write((ushort)(channels * 2));
        w.Write((ushort)bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(samples.Length * 2);
        foreach (var s in samples)
        {
            w.Write(s);
        }
        return m.ToArray();
    }

    [Fact]
    public void Wav_Decodes16BitPcm_SkippingOtherChunks()
    {
        var d = WavData.Decode(Wav(new short[] { 0, 16384, -32768, 32767 }, 2, 22050));

        Assert.Equal(22050, d.Rate);
        Assert.Equal(2, d.Channels);
        Assert.Equal(new[] { 0f, 0.5f, -1f, 32767 / 32768f }, d.Samples);
        Assert.Equal(2f / 22050f, d.Seconds, 6);
    }

    [Fact]
    public void Wav_RefusesCompressedAudio()
    {
        Assert.Throws<InvalidDataException>(() => WavData.Decode(Wav(new short[] { 1, 2 }, 1, 22050, format: 2)));
        Assert.Throws<InvalidDataException>(() => WavData.Decode(Encoding.ASCII.GetBytes("RIFX0000WAVE")));
    }

    /// <summary>A VTF 7.2 header (80 bytes) with DXT1 frames, a 4×4 thumbnail and one smaller mip.</summary>
    private static byte[] Vtf(int w, int h, byte[][] frameBlocks, int mips = 1, int format = 13)
    {
        var m = new MemoryStream();
        var b = new BinaryWriter(m);
        b.Write(Encoding.ASCII.GetBytes("VTF\0"));
        b.Write(7u);
        b.Write(2u);
        b.Write(80u);
        b.Write((ushort)w);
        b.Write((ushort)h);
        b.Write(0u);
        b.Write((ushort)frameBlocks.Length);
        b.Write((ushort)0);
        while (m.Length < 52)
        {
            b.Write((byte)0);
        }
        b.Write(format);
        b.Write((byte)mips);
        b.Write(13);
        b.Write((byte)4);
        b.Write((byte)4);
        while (m.Length < 80)
        {
            b.Write((byte)0);
        }
        b.Write(new byte[VtfData.Dxt1Size(4, 4)]); // thumbnail
        for (var mip = mips - 1; mip >= 1; mip--)
        {
            foreach (var _ in frameBlocks)
            {
                b.Write(Enumerable.Repeat((byte)0xEE, VtfData.Dxt1Size(Math.Max(1, w >> mip), Math.Max(1, h >> mip))).ToArray());
            }
        }
        foreach (var f in frameBlocks)
        {
            b.Write(f);
        }
        return m.ToArray();
    }

    /// <summary>One DXT1 block: c0 pure red, c1 black, all texels index 0 (red) except texel 5 = index 1 (black).</summary>
    private static byte[] RedBlock() => new byte[] { 0x00, 0xF8, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00 };

    [Fact]
    public void Vtf_DecodesEveryDxt1Frame_FromTheLargestMip()
    {
        var black = new byte[8];
        var vtf = Vtf(4, 4, new[] { RedBlock(), black }, mips: 2);

        var d = VtfData.Decode(vtf);

        Assert.Equal(2, d.Frames.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, d.Frames[0].Take(4).ToArray());
        Assert.Equal(new byte[] { 0, 0, 0, 255 }, d.Frames[0].Skip(5 * 4).Take(4).ToArray());
        Assert.All(Enumerable.Range(0, 16), i => Assert.Equal(0, d.Frames[1][i * 4]));
    }

    [Fact]
    public void Vtf_RefusesOtherFormats_AndShortFiles()
    {
        Assert.Throws<InvalidDataException>(() => VtfData.Decode(Vtf(4, 4, new[] { RedBlock() }, format: 12)));
        var cut = Vtf(4, 4, new[] { RedBlock() });
        Assert.Throws<InvalidDataException>(() => VtfData.Decode(cut.Take(cut.Length - 4).ToArray()));
    }

    [Fact]
    public void Dxt1_ThreeColourMode_HasTransparentBlack()
    {
        // c0 <= c1: index 3 = transparent black
        var block = new byte[] { 0x00, 0x00, 0x00, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF };

        var rgba = VtfData.DecodeDxt1(block, 0, 4, 4);

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, rgba.Take(4).ToArray());
    }

    [Fact]
    public void FlipRows_ReversesRows()
    {
        var rgba = new byte[] { 1, 1, 1, 1, 2, 2, 2, 2 };

        Assert.Equal(new byte[] { 2, 2, 2, 2, 1, 1, 1, 1 }, VtfData.FlipRows(rgba, 1, 2));
    }

    [Fact]
    public void Locator_ReadsLibraryPaths_FromVdf()
    {
        const string vdf = "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"C:\\\\Program Files (x86)\\\\Steam\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"/run/media/u/Games/Steam\"\n\t}\n}";

        Assert.Equal(new[] { "C:\\Program Files (x86)\\Steam", "/run/media/u/Games/Steam" }, DarkMessiahLocator.LibraryPaths(vdf));
        Assert.Empty(DarkMessiahLocator.LibraryPaths(null));
    }

    [Fact]
    public void Locator_MapsLinuxPathsToWinesZDrive()
    {
        Assert.Equal("Z:\\run\\media\\u\\Steam", DarkMessiahLocator.FromUnix("/run/media/u/Steam"));
        Assert.Equal("C:\\Steam", DarkMessiahLocator.FromUnix("C:\\Steam"));
    }

    [Fact]
    public void Locator_TriesBesideBas_ThenEveryLibrary_ThenTheUsualNamesOnEveryDrive()
    {
        var files = new Dictionary<string, string>
        {
            [Path.Combine("Z:\\home\\u\\Steam", "steamapps", "libraryfolders.vdf")] = "\"path\" \"/mnt/lib\"",
        };
        string Read(string p) => files.TryGetValue(p, out var t) ? t : throw new IOException("missing");

        var c = DarkMessiahLocator.Candidates(new[] { "/home/u/Steam", null }, Read, "common", new[] { "D:" }, wine: true);

        var game = DarkMessiahFiles.GameFolderName;
        Assert.Equal(Path.Combine("common", game), c[0]);
        Assert.Contains(Path.Combine("Z:\\home\\u\\Steam", "steamapps", "common", game), c);
        Assert.Contains(Path.Combine("Z:\\mnt\\lib", "steamapps", "common", game), c);
        Assert.Contains(Path.Combine("D:", "SteamLibrary", "steamapps", "common", game), c);
        Assert.Equal(c.Count, c.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Locator_WithoutWine_KeepsPathsAsTheyAre()
    {
        var c = DarkMessiahLocator.Candidates(new[] { "C:\\Steam" }, _ => null, null, Array.Empty<string>(), wine: false);

        Assert.Equal(new[] { Path.Combine("C:\\Steam", "steamapps", "common", DarkMessiahFiles.GameFolderName) }, c);
    }

    /// <summary>The real files, when the test machine has Dark Messiah (DMCS_DM_DIR = its install folder); also dumps its frames for comparison pictures.</summary>
    [Fact]
    public void RealFiles_WhenInstalled()
    {
        var game = Environment.GetEnvironmentVariable("DMCS_DM_DIR");
        if (string.IsNullOrEmpty(game) || !Directory.Exists(game))
        {
            return;
        }
        var found = new Dictionary<string, byte[]>();
        foreach (var dirPath in Directory.GetFiles(Path.Combine(game, "vpks"), "*_dir.vpk"))
        {
            var dir = File.ReadAllBytes(dirPath);
            var vpk = VpkDirectory.Parse(dir);
            foreach (var want in new[] { DarkMessiahFiles.ReadySoundPath, DarkMessiahFiles.HeartbeatPath, DarkMessiahFiles.VignettePath })
            {
                var e = vpk.Find(want);
                if (e != null)
                {
                    found[want] = vpk.Read(e, dir, (i, off, len) =>
                    {
                        using var fs = File.OpenRead(VpkDirectory.ArchivePath(dirPath, i));
                        fs.Position = off;
                        var buf = new byte[len];
                        fs.ReadExactly(buf);
                        return buf;
                    });
                }
            }
        }
        var ready = WavData.Decode(found[DarkMessiahFiles.ReadySoundPath]);
        var heart = WavData.Decode(found[DarkMessiahFiles.HeartbeatPath]);
        var vig = VtfData.Decode(found[DarkMessiahFiles.VignettePath]);
        Assert.Equal(2, ready.Channels);
        Assert.InRange(ready.Seconds, 2.2f, 2.4f);
        Assert.Equal(1, heart.Channels);
        Assert.Equal(25, vig.Frames.Length);
        Assert.Equal(256, vig.Width);

        var dump = Environment.GetEnvironmentVariable("DMCS_DUMP_DIR");
        if (!string.IsNullOrEmpty(dump))
        {
            Directory.CreateDirectory(dump);
            for (var f = 0; f < vig.Frames.Length; f++)
            {
                SignalDumps.WritePpm(Path.Combine(dump, $"dm_vig{f:00}.ppm"), vig.Frames[f], vig.Width);
            }
        }
    }
}
