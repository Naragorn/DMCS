using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DMCS.Core;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Dark Messiah's own ready sound, heartbeat and vignette frames, read from the player's own Dark Messiah install when
    /// the player picks "Dark Messiah" in the Adrenaline style options. DMCS ships none of them. Searched once per game
    /// start; read once, on a background thread (file reading and the DXT1 decoding must not stall a VR frame).
    /// </summary>
    internal static class DarkMessiahAssets
    {
        private const string Channel = "Adrenaline";

        private static Task _task;
        private static string _gameDir;
        private static WavData _ready;
        private static WavData _heartbeat;
        private static VtfData _vignette;
        private static WavData[] _versus = new WavData[0];
        private static WavData _disarm;

        /// <summary>Starts searching and reading in the background (once). Call on the main thread.</summary>
        public static void Begin()
        {
            if (_task != null)
            {
                return;
            }
            var common = CommonFolder();
            _task = Task.Run(() =>
            {
                try
                {
                    LoadAll(common);
                }
                catch (Exception ex)
                {
                    Dmcs.Log.Info("Dark Messiah: reading failed: " + ex.Message);
                }
            });
        }

        /// <summary>Dark Messiah's ready sound, or null; waits for the background read if it still runs.</summary>
        public static WavData ReadySound
        {
            get
            {
                Begin();
                _task.Wait();
                return _ready;
            }
        }

        /// <summary>False while still reading; then the heartbeat or null.</summary>
        public static bool TryHeartbeat(out WavData heartbeat)
        {
            Begin();
            heartbeat = _task.IsCompleted ? _heartbeat : null;
            return _task.IsCompleted;
        }

        /// <summary>False while still reading; then the vignette frames or null.</summary>
        public static bool TryVignette(out VtfData vignette)
        {
            Begin();
            vignette = _task.IsCompleted ? _vignette : null;
            return _task.IsCompleted;
        }

        /// <summary>False while still reading; then Dark Messiah's blade lock takes (maybe none) and its disarm sound (or null).</summary>
        public static bool TryClashSounds(out WavData[] versus, out WavData disarm)
        {
            Begin();
            var done = _task.IsCompleted;
            versus = done ? _versus : new WavData[0];
            disarm = done ? _disarm : null;
            return done;
        }

        private static void LoadAll(string common)
        {
            var dir = Find(common);
            if (dir == null)
            {
                return;
            }
            var watch = Stopwatch.StartNew();
            var wanted = new[] { DarkMessiahFiles.ReadySoundPath, DarkMessiahFiles.HeartbeatPath, DarkMessiahFiles.VignettePath, DarkMessiahFiles.DisarmPath }
                .Concat(DarkMessiahFiles.VersusPaths).ToArray();
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
            _versus = DarkMessiahFiles.VersusPaths.Select(p => Decode(found, p, WavData.Decode)).Where(w => w != null).ToArray();
            _disarm = Decode(found, DarkMessiahFiles.DisarmPath, WavData.Decode);
            Dmcs.Log.Info("Dark Messiah files read in " + watch.ElapsedMilliseconds + " ms: ready sound " + Describe(_ready)
                + ", heartbeat " + Describe(_heartbeat) + ", vignette " + (_vignette == null ? "missing" : _vignette.Frames.Length + " frames")
                + ", blade lock " + _versus.Length + " takes, disarm " + Describe(_disarm));
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
        private static string Find(string common)
        {
            List<string> candidates;
            try
            {
                var wine = IsWine();
                candidates = DarkMessiahLocator.Candidates(SteamRoots(), ReadText, common, DriveRoots(wine), wine);
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

        /// <summary>steamapps\common, where Blade &amp; Sorcery itself is installed (Application.dataPath: main thread only).</summary>
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
