using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Clash of Might (blade lock). Step-1 skeleton: Mod Menu group + live on/off state only; mechanic comes in its build step.</summary>
    public static class ClashOfMight
    {
        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ClashOfMight, true, Dmcs.Log);

        [ModOption(
            name = DmcsCategories.ClashOfMight + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Blade lock: push back with small controller movements to win and disarm the enemy.",
            category = DmcsCategories.ClashOfMight,
            categoryOrder = DmcsCategories.ClashOfMightOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }
    }
}
