using System;
using System.Linq;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Sets another DMCS option from code the way the mods page does, so value, save and page row agree.</summary>
    internal static class OptionSync
    {
        /// <returns>false when the option or the value is not available (mod data not loaded yet).</returns>
        public static bool Apply(string optionName, object value)
        {
            var option = DmcsScript.LoadedMod?.modOptions?.FirstOrDefault(o => o.name == optionName);
            if (option?.parameterValues == null)
            {
                return false;
            }
            var index = IndexOf(option.parameterValues.Select(p => p.value).ToArray(), value);
            if (index < 0)
            {
                Dmcs.Log.Info("Option '" + optionName + "' has no value '" + value + "'.");
                return false;
            }
            if (option.currentValueIndex != index)
            {
                // ModOption.Apply: saves the index, sets the member, raises ValueChanged.
                option.Apply(index);
            }
            RefreshRow(option);
            return true;
        }

        /// <summary>
        /// Exact match, or for floats the nearest value within 0.001: the slider values are start + i × step, and the
        /// game's Mono can round them differently from the compiler's constant (seen 2026-11-17: 0.1 + 7 × 0.05 ≠ 0.45f).
        /// </summary>
        private static int IndexOf(object[] values, object value)
        {
            var exact = Array.FindIndex(values, v => Equals(v, value));
            if (exact >= 0 || !(value is float f))
            {
                return exact;
            }
            var best = -1;
            var bestDiff = 0.001f;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is float v && Math.Abs(v - f) < bestDiff)
                {
                    best = i;
                    bestDiff = Math.Abs(v - f);
                }
            }
            return best;
        }

        /// <summary>An open mods page shows the new value (UIList.LoadValue(false) reads the saved index).</summary>
        private static void RefreshRow(ModOption option)
        {
            var ui = option.uiComponent;
            if (!(ui is UIList list) || list == null)
            {
                return;
            }
            list.LoadValue(isDefaultValue: false);
            list.Refresh();
        }
    }
}
