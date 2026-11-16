using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Adrenaline. Step-1 skeleton: Mod Menu group + live on/off state only; mechanic comes in its build step.</summary>
    public static class Adrenaline
    {
        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.Adrenaline, true, Dmcs.Log);

        [ModOption(
            name = DmcsCategories.Adrenaline + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Combat fills a meter; when full, your next hit is a critical.",
            category = DmcsCategories.Adrenaline,
            categoryOrder = DmcsCategories.AdrenalineOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }
    }
}
