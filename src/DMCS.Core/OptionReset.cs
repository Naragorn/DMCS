using System;
using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>One Mod Menu option as seen by the reset logic (adapter over ThunderRoad.ModOption).</summary>
    public interface IResettableOption
    {
        string Name { get; }

        /// <summary>Index of the default value, as computed by the game (-1 = not found).</summary>
        int DefaultIndex { get; }

        int ValueCount { get; }

        void ApplyIndex(int index);

        /// <summary>Redraw the menu row if it exists; must tolerate "menu never opened".</summary>
        void RefreshUi();
    }

    public static class OptionReset
    {
        /// <summary>
        /// Mirrors the game's own fallback in ModOption.Apply: an unknown or out-of-range
        /// default falls back to the first value. Returns -1 when there is nothing to apply.
        /// </summary>
        public static int ResolveDefaultIndex(int defaultIndex, int valueCount)
        {
            if (valueCount <= 0)
            {
                return -1;
            }
            return defaultIndex >= 0 && defaultIndex < valueCount ? defaultIndex : 0;
        }

        /// <summary>Applies every option's default (except <paramref name="excludeName"/>). Returns how many were reset.</summary>
        public static int ResetAll(IEnumerable<IResettableOption> options, string excludeName, DmcsLog log)
        {
            if (options == null)
            {
                return 0;
            }
            var count = 0;
            foreach (var option in options)
            {
                if (option == null || option.Name == excludeName)
                {
                    continue;
                }
                var index = ResolveDefaultIndex(option.DefaultIndex, option.ValueCount);
                if (index < 0)
                {
                    continue;
                }
                try
                {
                    option.ApplyIndex(index);
                    option.RefreshUi();
                    count++;
                }
                catch (Exception ex)
                {
                    log?.Info("Reset failed for option '" + option.Name + "': " + ex.Message);
                }
            }
            return count;
        }
    }
}
