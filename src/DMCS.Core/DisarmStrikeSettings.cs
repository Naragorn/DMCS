using System.Linq;

namespace DMCS.Core
{
    /// <summary>Disarm Strike's Mod Menu names, ranges and defaults (single source of truth).</summary>
    public static class DisarmStrikeSettings
    {
        public const string RequireStaggerName = "Disarm Requires Stagger";
        public const string IncludeForearmName = "Disarm Include Forearm";
        public const string DropBothHandsName = "Disarm Drops Both Hands";
        public const string ChanceName = "Disarm Chance";
        public const string NoRegrabTimeName = "Disarm No Regrab Time";
        public const string StayDisarmedName = "Disarm Stays Permanent";
        public const string StandStillName = "Disarmed Stand Still";

        public const bool DefaultRequireStagger = true;
        public const bool DefaultIncludeForearm = false;
        public const bool DefaultDropBothHands = true;
        public const int DefaultChancePercent = 100;
        public const bool DefaultStayDisarmed = false;
        public const bool DefaultStandStill = true;

        public const string BodyHitDropName = "Body Hit Drop";
        public const string BodyHitDropChanceName = "Body Hit Drop Chance";
        public const string DropChancePerExtraHitName = "Drop Chance Per Extra Hit";
        public const string LoosenedSparkName = "Loosened Grip Spark";
        public const string LoosenedHapticsName = "Loosened Grip Haptics";
        public const string ShieldsName = "Shields Can Be Disarmed";
        public const string FlingForceName = "Disarm Fling Force";

        public const bool DefaultBodyHitDrop = true;
        public const int DefaultBodyHitDropChance = 25;
        public const int DefaultDropChancePerExtraHit = 25;
        public const bool DefaultLoosenedSpark = true;
        public const bool DefaultLoosenedHaptics = true;
        public const bool DefaultShieldsCanBeDisarmed = false;

        public const string FemaleChanceName = "Body Hit Drop Chance Female";
        public const string SmallerChanceName = "Body Hit Drop Chance Smaller";
        public const string SmallerMarginName = "Smaller By";

        /// <summary>Same as the base chance, so the type chances change nothing until set.</summary>
        public const int DefaultFemaleChance = DefaultBodyHitDropChance;

        public const int DefaultSmallerChance = DefaultBodyHitDropChance;

        /// <summary>How much shorter than the player (cm) an NPC must be to count as smaller; 0 = any shorter.</summary>
        public static readonly SliderSpec SmallerMargin = new SliderSpec(SmallerMarginName, 0f, 50f, 5f, 0f);

        /// <summary>Velocity change (m/s) a weapon knocked out of the hand gets in the hit's direction; 0 = just drops (default a first guess).</summary>
        public static readonly SliderSpec FlingForce = new SliderSpec(FlingForceName, 0f, 10f, 0.5f, 3f);

        /// <summary>The game's metal collision effect (catalog Effect_HitMaterialMetal: metal sound + Bas.Particle.Collision.Metal).</summary>
        public const string SparkEffectId = "HitMaterialMetal";

        /// <summary>Seconds the NPC neither picks up nor draws a weapon (and stands still).</summary>
        public static readonly SliderSpec NoRegrabTime = new SliderSpec(NoRegrabTimeName, 2f, 60f, 2f, 10f);

        /// <summary>
        /// The game's own brain the disarmed NPC gets while it stands still. Seen in BasHarness runs
        /// (2026-11-16): an NPC with it stands and does not attack, and it has BrainModuleHitReaction (staggers).
        /// Not yet seen: that it never draws or picks up a weapon (DMCS also releases any grab while disarmed).
        /// </summary>
        public const string PassiveBrainId = "HumanDummy";

        public static int[] ChanceValues() => Enumerable.Range(0, 21).Select(i => i * 5).ToArray();

        public static float NoRegrabSeconds(bool permanent, float seconds)
        {
            return permanent ? float.PositiveInfinity : seconds;
        }
    }
}
