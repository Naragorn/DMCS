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
            var index = Array.FindIndex(option.parameterValues, p => Equals(p.value, value));
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
