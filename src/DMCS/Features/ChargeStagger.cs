using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Charge Stagger. Step-1 skeleton: Mod Menu group + live on/off state only; mechanic comes in its build step.</summary>
    public static class ChargeStagger
    {
        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ChargeStagger, true, Dmcs.Log);

        [ModOption(
            name = DmcsCategories.ChargeStagger + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Melee hits landed while sprinting stagger the enemy.",
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }
    }
}
