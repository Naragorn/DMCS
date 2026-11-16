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
