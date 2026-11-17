using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DMCS.Core;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Dark Messiah's own ready sound, heartbeat and vignette frames, read from the player's own Dark Messiah install when
    /// the player picks "Dark Messiah" in the Adrenaline style options. DMCS ships none of them. Searched once per game
    /// start; read once on first use.
    /// </summary>
    internal static class DarkMessiahAssets
    {
        private const string Channel = "Adrenaline";

        private static bool _searched;
        private static string _gameDir;
        private static bool _loaded;
        private static WavData _ready;
        private static WavData _heartbeat;
        private static VtfData _vignette;

        public static WavData ReadySound => Load() ? _ready : null;
        public static WavData Heartbeat => Load() ? _heartbeat : null;
        public static VtfData Vignette => Load() ? _vignette : null;

        private static bool Load()
        {
            if (_loaded)
            {
                return true;
            }
            if (_searched && _gameDir == null)
            {
                return false;
            }
            var dir = Find();
            if (dir == null)
            {
                return false;
            }
            var watch = Stopwatch.StartNew();
            var wanted = new[] { DarkMessiahFiles.ReadySoundPath, DarkMessiahFiles.HeartbeatPath, DarkMessiahFiles.VignettePath };
            var found = new Dictionary<string, byte[]>();
            foreach (var dirPath in Directory.GetFiles(Path.Combine(dir, "vpks"), "*_dir.vpk"))
            {
                try
                {
                    var bytes = File.ReadAllBytes(dirPath);
                    var vpk = VpkDirectory.Parse(bytes);
                    foreach (var path in wanted.Where(w => !found.ContainsKey(w)))
                    {
                        var entry = vpk.Find(path);
                        if (entry != null)
                        {
                            found[path] = vpk.Read(entry, bytes, (index, offset, length) => ReadArchive(dirPath, index, offset, length));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Dmcs.Log.Info("Dark Messiah: could not read " + Path.GetFileName(dirPath) + ": " + ex.Message);
                }
            }
            _ready = Decode(found, DarkMessiahFiles.ReadySoundPath, WavData.Decode);
            _heartbeat = Decode(found, DarkMessiahFiles.HeartbeatPath, WavData.Decode);
            _vignette = Decode(found, DarkMessiahFiles.VignettePath, VtfData.Decode);
            _loaded = true;
            Dmcs.Log.Info("Dark Messiah files read in " + watch.ElapsedMilliseconds + " ms: ready sound " + Describe(_ready)
                + ", heartbeat " + Describe(_heartbeat) + ", vignette " + (_vignette == null ? "missing" : _vignette.Frames.Length + " frames"));
            return true;
        }

        private static string Describe(WavData w) =>
            w == null ? "missing" : w.Seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s";

        private static T Decode<T>(Dictionary<string, byte[]> found, string path, Func<byte[], T> decode) where T : class
        {
            if (!found.TryGetValue(path, out var bytes))
            {
                return null;
            }
            try
            {
                return decode(bytes);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Dark Messiah: could not decode " + path + ": " + ex.Message);
                return null;
            }
        }

        private static byte[] ReadArchive(string dirPath, int index, long offset, int length)
        {
            using (var fs = File.OpenRead(VpkDirectory.ArchivePath(dirPath, index)))
            {
                fs.Position = offset;
                var buffer = new byte[length];
                var read = 0;
                while (read < length)
                {
                    var n = fs.Read(buffer, read, length - read);
                    if (n <= 0)
                    {
                        throw new EndOfStreamException();
                    }
                    read += n;
                }
                return buffer;
            }
        }

        /// <summary>The first candidate folder that has Dark Messiah's "vpks" folder; null when there is none.</summary>
        private static string Find()
        {
            _searched = true;
            List<string> candidates;
            try
            {
                var wine = IsWine();
                candidates = DarkMessiahLocator.Candidates(SteamRoots(), ReadText, CommonFolder(), DriveRoots(wine), wine);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Dark Messiah: search failed: " + ex.Message);
                return null;
            }
            _gameDir = candidates.FirstOrDefault(c => SafeExists(Path.Combine(c, "vpks")));
            Dmcs.Log.Info(_gameDir != null
                ? "Dark Messiah found: " + _gameDir
                : "Dark Messiah not found (" + candidates.Count + " folders checked); its options fall back to DMCS 3 / Blade & Sorcery.");
            return _gameDir;
        }

        private static bool SafeExists(string dir)
        {
            try
            {
                return Directory.Exists(dir);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        /// <summary>Proton sets STEAM_COMPAT_* for the game; Wine also keeps its own registry key.</summary>
        private static bool IsWine()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH")))
            {
                return true;
            }
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Wine"))
                {
                    return key != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The Linux Steam (under Proton) and the Windows Steam (registry).</summary>
        private static IEnumerable<string> SteamRoots()
        {
            var roots = new List<string> { Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH") };
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                {
                    roots.Add(key?.GetValue("SteamPath") as string);
                }
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam"))
                {
                    roots.Add(key?.GetValue("InstallPath") as string);
                }
            }
            catch (Exception)
            {
                // no registry access: the other roots still count
            }
            return roots.Where(r => !string.IsNullOrEmpty(r)).Select(r => r.Replace('/', r.StartsWith("/", StringComparison.Ordinal) ? '/' : '\\'));
        }

        /// <summary>steamapps\common, where Blade &amp; Sorcery itself is installed.</summary>
        private static string CommonFolder()
        {
            try
            {
                return Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Every ready drive; under Wine also the Linux mounts reached through Z:.</summary>
        private static IEnumerable<string> DriveRoots(bool wine)
        {
            var roots = new List<string>();
            try
            {
                roots.AddRange(DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed).Select(d => d.RootDirectory.FullName));
            }
            catch (Exception)
            {
                // no drive list: the mounts below still count
            }
            if (wine)
            {
                foreach (var parent in new[] { "Z:\\run\\media", "Z:\\media" })
                {
                    foreach (var user in Subfolders(parent))
                    {
                        roots.AddRange(Subfolders(user));
                    }
                }
                roots.AddRange(Subfolders("Z:\\mnt"));
                roots.AddRange(Subfolders("Z:\\home"));
            }
            return roots;
        }

        private static IEnumerable<string> Subfolders(string dir)
        {
            try
            {
                return Directory.Exists(dir) ? Directory.GetDirectories(dir) : Enumerable.Empty<string>();
            }
            catch (Exception)
            {
                return Enumerable.Empty<string>();
            }
        }
    }
}
