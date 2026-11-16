using DMCS.Core;

namespace DMCS
{
    /// <summary>All feature toggles, in spec order.</summary>
    public static class Features
    {
        public static FeatureToggle[] All => new[]
        {
            ClashOfMight.Toggle,
            DisarmStrike.Toggle,
            ChargeStagger.Toggle,
            Adrenaline.Toggle,
        };
    }
}
