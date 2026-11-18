using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DMCS.Core
{
    /// <summary>
    /// Readers for the few Dark Messiah files the Adrenaline signals can use when the player picks "Dark Messiah" and has
    /// the game installed. DMCS ships none of them; they are read from the player's own install at runtime.
    /// Formats as found in Dark Messiah's files (2026-11-17): Valve VPK version 2 directories with numbered archives,
    /// 16-bit PCM WAV, VTF 7.2 with DXT1 frames.
    /// </summary>
    public static class DarkMessiahFiles
    {
        public const string GameFolderName = "Dark Messiah Might and Magic Single Player";
        public const string ReadySoundPath = "mm/sound/arkane/character_sheet/adrenaline_reached.wav";
        public const string HeartbeatPath = "mm/sound/arkane/player/player_heartbeat01.wav";

        /// <summary>
        /// Dark Messiah's blade lock sound "Sword_Versus_Sword" (three takes, volume 0.5–0.7, pitch 95–105) and its "Sword_Disarm"
        /// (volume 0.6), read in mm/scripts/game_sounds_sword.txt 2026-11-18; 16-bit PCM, 22050 Hz, mono.
        /// </summary>
        public static readonly string[] VersusPaths =
        {
            "mm/sound/arkane/weapons/sword/sword_vs_sword01.wav",
            "mm/sound/arkane/weapons/sword/sword_vs_sword02.wav",
            "mm/sound/arkane/weapons/sword/sword_vs_sword03.wav",
        };

        public const string DisarmPath = "mm/sound/arkane/weapons/sword/sword_disarm.wav";
        public const string VignettePath = "mm/materials/hud/fullscreen_adrenaline2_.vtf";

        /// <summary>Dark Messiah plays Adrenaline_Reached at 0.6 and its heartbeat at 0.5 (its sound scripts).</summary>
        public const float ReadySoundVolume = 0.6f;
    }

    /// <summary>One file listed in a VPK directory.</summary>
    public sealed class VpkEntry
    {
        public VpkEntry(string path, ushort archiveIndex, uint offset, uint length, byte[] preload)
        {
            Path = path;
            ArchiveIndex = archiveIndex;
            Offset = offset;
            Length = length;
            Preload = preload;
        }

        public string Path { get; }

        /// <summary>Which numbered archive holds the data; 0x7fff = the directory file itself, after the tree.</summary>
        public ushort ArchiveIndex { get; }

        public uint Offset { get; }
        public uint Length { get; }

        /// <summary>Bytes stored in the tree ahead of the archive data.</summary>
        public byte[] Preload { get; }
    }

    /// <summary>A Valve VPK directory (version 1 or 2): the file tree of a "*_dir.vpk".</summary>
    public sealed class VpkDirectory
    {
        public const ushort InDirectory = 0x7fff;
        private const uint Signature = 0x55aa1234;

        private readonly Dictionary<string, VpkEntry> _entries;

        private VpkDirectory(Dictionary<string, VpkEntry> entries, int dataStart)
        {
            _entries = entries;
            DataStart = dataStart;
        }

        /// <summary>Where the data of entries with <see cref="InDirectory"/> starts in the directory file.</summary>
        public int DataStart { get; }

        public int Count => _entries.Count;

        public static VpkDirectory Parse(byte[] dir)
        {
            using (var r = new BinaryReader(new MemoryStream(dir), Encoding.ASCII))
            {
                if (r.ReadUInt32() != Signature)
                {
                    throw new InvalidDataException("not a VPK directory");
                }
                var version = r.ReadUInt32();
                var treeSize = r.ReadUInt32();
                if (version == 2)
                {
                    r.ReadBytes(16);
                }
                else if (version != 1)
                {
                    throw new InvalidDataException("VPK version " + version + " is not supported");
                }
                var headerSize = (int)r.BaseStream.Position;
                var entries = new Dictionary<string, VpkEntry>(StringComparer.OrdinalIgnoreCase);
                while (true)
                {
                    var ext = ReadString(r);
                    if (ext.Length == 0)
                    {
                        break;
                    }
                    while (true)
                    {
                        var folder = ReadString(r);
                        if (folder.Length == 0)
                        {
                            break;
                        }
                        while (true)
                        {
                            var name = ReadString(r);
                            if (name.Length == 0)
                            {
                                break;
                            }
                            r.ReadUInt32(); // CRC
                            var preloadSize = r.ReadUInt16();
                            var archive = r.ReadUInt16();
                            var offset = r.ReadUInt32();
                            var length = r.ReadUInt32();
                            r.ReadUInt16(); // terminator 0xffff
                            var preload = r.ReadBytes(preloadSize);
                            var path = (folder == " " ? string.Empty : folder + "/") + name + (ext == " " ? string.Empty : "." + ext);
                            entries[path] = new VpkEntry(path, archive, offset, length, preload);
                        }
                    }
                }
                return new VpkDirectory(entries, headerSize + (int)treeSize);
            }
        }

        public VpkEntry Find(string path)
        {
            _entries.TryGetValue(path.Replace('\\', '/').TrimStart('/'), out var e);
            return e;
        }

        /// <summary>The file's bytes: its preload plus the archive data, read through <paramref name="readArchive"/>(index, offset, length).</summary>
        public byte[] Read(VpkEntry entry, byte[] dirFile, Func<int, long, int, byte[]> readArchive)
        {
            byte[] data;
            if (entry.Length == 0)
            {
                data = new byte[0];
            }
            else if (entry.ArchiveIndex == InDirectory)
            {
                data = new byte[entry.Length];
                Array.Copy(dirFile, DataStart + entry.Offset, data, 0, entry.Length);
            }
            else
            {
                data = readArchive(entry.ArchiveIndex, entry.Offset, (int)entry.Length);
            }
            return entry.Preload.Concat(data).ToArray();
        }

        /// <summary>The archive file next to a directory file: "x_dir.vpk" → "x_007.vpk".</summary>
        public static string ArchivePath(string dirPath, int index) =>
            dirPath.Substring(0, dirPath.Length - "dir.vpk".Length) + index.ToString("000") + ".vpk";

        private static string ReadString(BinaryReader r)
        {
            var sb = new StringBuilder();
            while (true)
            {
                var b = r.ReadByte();
                if (b == 0)
                {
                    return sb.ToString();
                }
                sb.Append((char)b);
            }
        }
    }

    /// <summary>PCM audio from a WAV file.</summary>
    public sealed class WavData
    {
        public WavData(int rate, int channels, float[] samples)
        {
            Rate = rate;
            Channels = channels;
            Samples = samples;
        }

        public int Rate { get; }
        public int Channels { get; }

        /// <summary>Interleaved, -1..1.</summary>
        public float[] Samples { get; }

        public float Seconds => Samples.Length / (float)Channels / Rate;

        /// <summary>Reads 16-bit or 8-bit PCM; anything else (ADPCM, MP3) is refused.</summary>
        public static WavData Decode(byte[] wav)
        {
            using (var r = new BinaryReader(new MemoryStream(wav), Encoding.ASCII))
            {
                if (new string(r.ReadChars(4)) != "RIFF")
                {
                    throw new InvalidDataException("not a RIFF file");
                }
                r.ReadUInt32();
                if (new string(r.ReadChars(4)) != "WAVE")
                {
                    throw new InvalidDataException("not a WAVE file");
                }
                int format = 0, channels = 0, rate = 0, bits = 0;
                while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
                {
                    var id = new string(r.ReadChars(4));
                    var size = r.ReadInt32();
                    var next = r.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        format = r.ReadUInt16();
                        channels = r.ReadUInt16();
                        rate = r.ReadInt32();
                        r.ReadInt32();
                        r.ReadUInt16();
                        bits = r.ReadUInt16();
                    }
                    else if (id == "data")
                    {
                        if (format != 1 || (bits != 16 && bits != 8) || channels <= 0 || rate <= 0)
                        {
                            throw new InvalidDataException("only 8/16-bit PCM is supported (format " + format + ", " + bits + " bit)");
                        }
                        var available = (int)Math.Min(size, r.BaseStream.Length - r.BaseStream.Position);
                        var bytes = r.ReadBytes(available);
                        var samples = bits == 16 ? new float[bytes.Length / 2] : new float[bytes.Length];
                        for (var i = 0; i < samples.Length; i++)
                        {
                            samples[i] = bits == 16 ? BitConverter.ToInt16(bytes, i * 2) / 32768f : (bytes[i] - 128) / 128f;
                        }
                        return new WavData(rate, channels, samples);
                    }
                    r.BaseStream.Position = next;
                }
                throw new InvalidDataException("no data chunk");
            }
        }
    }

    /// <summary>The frames of a VTF texture, decoded to RGBA (top row first).</summary>
    public sealed class VtfData
    {
        private const int Dxt1 = 13;

        public VtfData(int width, int height, byte[][] frames)
        {
            Width = width;
            Height = height;
            Frames = frames;
        }

        public int Width { get; }
        public int Height { get; }
        public byte[][] Frames { get; }

        /// <summary>VTF 7.0–7.2, DXT1, the largest mip of every frame.</summary>
        public static VtfData Decode(byte[] vtf)
        {
            using (var r = new BinaryReader(new MemoryStream(vtf), Encoding.ASCII))
            {
                if (new string(r.ReadChars(4)) != "VTF\0")
                {
                    throw new InvalidDataException("not a VTF file");
                }
                var major = r.ReadUInt32();
                var minor = r.ReadUInt32();
                if (major != 7 || minor > 2)
                {
                    throw new InvalidDataException("VTF " + major + "." + minor + " is not supported");
                }
                var headerSize = r.ReadUInt32();
                var width = r.ReadUInt16();
                var height = r.ReadUInt16();
                r.ReadUInt32(); // flags
                var frameCount = Math.Max((ushort)1, r.ReadUInt16());
                r.BaseStream.Position = 52;
                var format = r.ReadInt32();
                var mips = r.ReadByte();
                var lowFormat = r.ReadInt32();
                var lowWidth = r.ReadByte();
                var lowHeight = r.ReadByte();
                if (format != Dxt1)
                {
                    throw new InvalidDataException("VTF format " + format + " is not supported (DXT1 only)");
                }
                long offset = headerSize;
                if (lowFormat == Dxt1 && lowWidth > 0 && lowHeight > 0)
                {
                    offset += Dxt1Size(lowWidth, lowHeight);
                }
                for (var m = mips - 1; m >= 1; m--)
                {
                    offset += frameCount * Dxt1Size(Math.Max(1, width >> m), Math.Max(1, height >> m));
                }
                var frameSize = Dxt1Size(width, height);
                if (offset + (frameCount * frameSize) > vtf.Length)
                {
                    throw new InvalidDataException("VTF is shorter than its frames");
                }
                var frames = new byte[frameCount][];
                for (var f = 0; f < frameCount; f++)
                {
                    frames[f] = DecodeDxt1(vtf, (int)(offset + (f * frameSize)), width, height);
                }
                return new VtfData(width, height, frames);
            }
        }

        public static int Dxt1Size(int width, int height) => Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * 8;

        /// <summary>Decodes DXT1 blocks to RGBA bytes, top row first; the 1-bit alpha colour becomes transparent black.</summary>
        public static byte[] DecodeDxt1(byte[] data, int start, int width, int height)
        {
            var rgba = new byte[width * height * 4];
            var blocksX = Math.Max(1, (width + 3) / 4);
            var blocksY = Math.Max(1, (height + 3) / 4);
            var palette = new byte[16];
            for (var by = 0; by < blocksY; by++)
            {
                for (var bx = 0; bx < blocksX; bx++)
                {
                    var o = start + (((by * blocksX) + bx) * 8);
                    var c0 = (ushort)(data[o] | (data[o + 1] << 8));
                    var c1 = (ushort)(data[o + 2] | (data[o + 3] << 8));
                    var bits = (uint)(data[o + 4] | (data[o + 5] << 8) | (data[o + 6] << 16) | (data[o + 7] << 24));
                    Rgb565(c0, palette, 0);
                    Rgb565(c1, palette, 4);
                    for (var ch = 0; ch < 3; ch++)
                    {
                        if (c0 > c1)
                        {
                            palette[8 + ch] = (byte)(((2 * palette[ch]) + palette[4 + ch]) / 3);
                            palette[12 + ch] = (byte)((palette[ch] + (2 * palette[4 + ch])) / 3);
                        }
                        else
                        {
                            palette[8 + ch] = (byte)((palette[ch] + palette[4 + ch]) / 2);
                            palette[12 + ch] = 0;
                        }
                    }
                    palette[3] = palette[7] = palette[11] = 255;
                    palette[15] = c0 > c1 ? (byte)255 : (byte)0;
                    for (var i = 0; i < 16; i++)
                    {
                        var x = (bx * 4) + (i % 4);
                        var y = (by * 4) + (i / 4);
                        if (x >= width || y >= height)
                        {
                            continue;
                        }
                        var idx = (int)((bits >> (2 * i)) & 3) * 4;
                        var p = ((y * width) + x) * 4;
                        rgba[p] = palette[idx];
                        rgba[p + 1] = palette[idx + 1];
                        rgba[p + 2] = palette[idx + 2];
                        rgba[p + 3] = palette[idx + 3];
                    }
                }
            }
            return rgba;
        }

        private static void Rgb565(ushort c, byte[] p, int at)
        {
            p[at] = (byte)(((c >> 11) & 31) * 255 / 31);
            p[at + 1] = (byte)(((c >> 5) & 63) * 255 / 63);
            p[at + 2] = (byte)((c & 31) * 255 / 31);
        }

        /// <summary>Rows reversed (top-first ↔ bottom-first, as Unity textures want).</summary>
        public static byte[] FlipRows(byte[] rgba, int width, int height)
        {
            var row = width * 4;
            var flipped = new byte[rgba.Length];
            for (var y = 0; y < height; y++)
            {
                Array.Copy(rgba, y * row, flipped, (height - 1 - y) * row, row);
            }
            return flipped;
        }
    }

    /// <summary>
    /// Where a Dark Messiah install may be: every Steam library known to Steam (its libraryfolders.vdf), the common
    /// folder next to Blade &amp; Sorcery, and the usual library folder names on every drive / mount. Under Proton the
    /// Linux paths are reached through Wine's Z: drive.
    /// </summary>
    public static class DarkMessiahLocator
    {
        private static readonly Regex VdfPath = new Regex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);

        /// <summary>Library folder names Steam uses by default, relative to a drive or mount root.</summary>
        public static readonly string[] LibraryNamesOnDrive =
        {
            "SteamLibrary", "Steam", "Program Files (x86)/Steam", "Program Files/Steam", "Games/Steam", "Games/SteamLibrary",
        };

        /// <summary>The "path" entries of a Steam libraryfolders.vdf (backslashes un-escaped).</summary>
        public static IEnumerable<string> LibraryPaths(string vdfText)
        {
            if (string.IsNullOrEmpty(vdfText))
            {
                yield break;
            }
            foreach (Match m in VdfPath.Matches(vdfText))
            {
                yield return m.Groups[1].Value.Replace("\\\\", "\\");
            }
        }

        /// <summary>A Linux path as Wine sees it on its Z: drive ("/a/b" → "Z:\a\b").</summary>
        public static string FromUnix(string path) =>
            string.IsNullOrEmpty(path) || !path.StartsWith("/", StringComparison.Ordinal) ? path : "Z:" + path.Replace('/', '\\');

        /// <summary>
        /// Candidate game folders, most likely first, without duplicates.
        /// <paramref name="steamRoots"/>: Steam installs (Linux paths are mapped to Z: when <paramref name="wine"/>);
        /// <paramref name="readFile"/> returns a file's text or null; <paramref name="commonFolder"/>: the steamapps\common
        /// folder Blade &amp; Sorcery runs from; <paramref name="driveRoots"/>: drive letters and mounts to try the usual names on.
        /// </summary>
        public static List<string> Candidates(
            IEnumerable<string> steamRoots, Func<string, string> readFile, string commonFolder, IEnumerable<string> driveRoots, bool wine)
        {
            var libraries = new List<string>();
            foreach (var root in steamRoots.Where(r => !string.IsNullOrEmpty(r)))
            {
                var rootPath = wine ? FromUnix(root) : root;
                libraries.Add(rootPath);
                foreach (var vdf in new[] { Path.Combine(rootPath, "steamapps", "libraryfolders.vdf"), Path.Combine(rootPath, "config", "libraryfolders.vdf") })
                {
                    string text = null;
                    try
                    {
                        text = readFile(vdf);
                    }
                    catch (Exception)
                    {
                        // unreadable: skip
                    }
                    libraries.AddRange(LibraryPaths(text).Select(p => wine ? FromUnix(p) : p));
                }
            }
            var games = new List<string>();
            if (!string.IsNullOrEmpty(commonFolder))
            {
                games.Add(Path.Combine(commonFolder, DarkMessiahFiles.GameFolderName));
            }
            games.AddRange(libraries.Select(l => Path.Combine(l, "steamapps", "common", DarkMessiahFiles.GameFolderName)));
            foreach (var drive in driveRoots.Where(d => !string.IsNullOrEmpty(d)))
            {
                games.AddRange(LibraryNamesOnDrive.Select(n =>
                    Path.Combine(drive, n.Replace('/', Path.DirectorySeparatorChar), "steamapps", "common", DarkMessiahFiles.GameFolderName)));
            }
            return games.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
