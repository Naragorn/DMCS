using System;
using System.Linq;

namespace DMCS.Core
{
    /// <summary>
    /// Which ready sound, vignette and heartbeat the Adrenaline signals use: every version DMCS built (to compare them in
    /// the headset), or Dark Messiah's own files read from the player's own Dark Messiah install (nothing is shipped).
    /// String dropdowns: enum dropdowns get a wrong default in B&amp;S.
    /// </summary>
    public static class AdrenalineStyles
    {
        public const string Dmcs1 = "DMCS 1";
        public const string Dmcs2 = "DMCS 2";
        public const string Dmcs3 = "DMCS 3";
        public const string Dmcs4 = "DMCS 4";
        public const string DarkMessiah = "Dark Messiah";
        public const string BladeAndSorcery = "Blade & Sorcery";

        public static readonly string[] SoundNames = { Dmcs1, Dmcs2, Dmcs3, DarkMessiah };
        public static readonly string[] VignetteNames = { Dmcs1, Dmcs2, Dmcs3, Dmcs4, DarkMessiah };
        public static readonly string[] HeartbeatNames = { BladeAndSorcery, DarkMessiah };

        public const string DefaultSound = Dmcs3;
        public const string DefaultVignette = Dmcs3;
        public const string DefaultHeartbeat = BladeAndSorcery;

        public static bool IsDarkMessiah(string name) => string.Equals(name, DarkMessiah, StringComparison.Ordinal);

        /// <summary>The synthesis for a DMCS version; DMCS 3 for anything else (also the fallback when Dark Messiah is missing).</summary>
        public static ReadySoundSpec Sound(string name)
        {
            switch (name)
            {
                case Dmcs1:
                    return ReadySoundSpec.Version1();
                case Dmcs2:
                    return ReadySoundSpec.Version2();
                default:
                    return ReadySoundSpec.Version3();
            }
        }

        /// <summary>The drawing of a DMCS version; DMCS 3 for anything else.</summary>
        public static VignetteStyle Vignette(string name)
        {
            switch (name)
            {
                case Dmcs1:
                    return VignetteStyle.Flames;
                case Dmcs2:
                    return VignetteStyle.Streaks;
                case Dmcs4:
                    return VignetteStyle.Embers;
                default:
                    return VignetteStyle.Patchy;
            }
        }

        /// <summary>
        /// The streaks, length and hue each DMCS version was built with. Choosing a version sets these sliders, so it looks
        /// as it was built; they can be changed afterwards. Dark Messiah's frames ignore them.
        /// </summary>
        public static (int streaks, float length, float hue) VignetteLook(string name)
        {
            switch (name)
            {
                case Dmcs1:
                    return (14, 0.35f, 12f);
                case Dmcs2:
                    return (40, 0.45f, 6f);
                case Dmcs4:
                    return (Dmcs4Streaks, Dmcs4Length, Dmcs4Hue);
                default:
                    return (AdrenalineSettings.DefaultVignetteStreaks, AdrenalineSettings.VignetteLength.Default, AdrenalineSettings.VignetteHue.Default);
            }
        }

        /// <summary>DMCS 4's look, tuned to Dark Messiah's frames (see VignetteFlipbook.EmberFrames).</summary>
        public const int Dmcs4Streaks = 90;
        public const float Dmcs4Length = 0.5f;
        public const float Dmcs4Hue = 10f;

        public static bool IsKnown(string[] names, string name) => names.Contains(name);
    }
}
