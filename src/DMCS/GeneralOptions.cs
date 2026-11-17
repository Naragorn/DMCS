using System.Linq;
using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Mod-wide Mod Menu entries: Debug Logging and Reset To Defaults.</summary>
    public static class GeneralOptions
    {
        [ModOption(
            name = DmcsCategories.DebugLoggingName,
            tooltip = "Logs clash start/end, stagger, disarm and meter changes to the console and Player.log.",
            category = DmcsCategories.General,
            categoryOrder = DmcsCategories.GeneralOrder,
            order = 0)]
        public static bool DebugLogging
        {
            get => Dmcs.Log.DebugEnabled;
            set
            {
                if (Dmcs.Log.DebugEnabled == value)
                {
                    return;
                }
                Dmcs.Log.DebugEnabled = value;
                Dmcs.Log.Info("Debug Logging " + (value ? "on" : "off"));
            }
        }

        [ModOption(
            name = PlayerStrikeRule.MinHandSpeedName,
            tooltip = "How fast (m/s) your real hand must move for a touch of your hand or arm to count as a punch. Stops enemies from disarming themselves by running into your hand. 0: every touch counts.",
            valueSourceName = nameof(MinHandSpeedValues),
            category = DmcsCategories.General,
            categoryOrder = DmcsCategories.GeneralOrder,
            order = 2)]
        [ModOptionSlider]
        public static float MinHandSpeed { get; set; } = PlayerStrikeRule.MinHandSpeed.Default;

        public static ModOptionFloat[] MinHandSpeedValues() =>
            PlayerStrikeRule.MinHandSpeed.Values().Select(v => new ModOptionFloat(v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m/s", v)).ToArray();

        /// <summary>Single-entry value list: a ButtonList with one value re-applies it on every press.</summary>
        public static ModOptionString[] ResetValues = { new ModOptionString("Reset", null, "reset") };

        [ModOption(
            name = DmcsCategories.ResetToDefaultsName,
            tooltip = "Sets every DMCS option back to its default value.",
            valueSourceName = nameof(ResetValues),
            category = DmcsCategories.General,
            categoryOrder = DmcsCategories.GeneralOrder,
            order = 1)]
        [ModOptionButton]
        [ModOptionDontSave]
        [ModOptionApplyOnPress]
        public static void ResetToDefaults(string _)
        {
            var mod = DmcsScript.LoadedMod;
            if (mod == null)
            {
                Dmcs.Log.Info("Reset To Defaults ignored: mod data not available yet.");
                return;
            }
            var options = mod.modOptions.Select(o => (IResettableOption)new ModOptionAdapter(o)).ToList();
            var count = OptionReset.ResetAll(options, DmcsCategories.ResetToDefaultsName, Dmcs.Log);
            Dmcs.Log.Info("Reset " + count + " options to defaults.");
        }
    }
}
