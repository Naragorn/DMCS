using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Disarm Strike. Step-1 skeleton: Mod Menu group + live on/off state only; mechanic comes in its build step.</summary>
    public static class DisarmStrike
    {
        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.DisarmStrike, true, Dmcs.Log);

        [ModOption(
            name = DmcsCategories.DisarmStrike + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Hitting a staggered enemy's hand makes them drop their weapon.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }
    }
}
