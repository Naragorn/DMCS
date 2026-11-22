using System;
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
        public const string StayDisarmedName = "Disarm Stays Permanent";
        public const string ShockChanceName = "Disarmed Shock Chance";
        public const string ShockTimeName = "Disarmed Shock Time";
        public const string BackOffChanceName = "Disarmed Back Off Chance";
        public const string BackOffTimeName = "Disarmed Back Off Time";
        public const string CowerChanceName = "Disarmed Cower Chance";
        public const string CowerTimeName = "Disarmed Cower Time";

        public const bool DefaultRequireStagger = true;
        public const bool DefaultIncludeForearm = false;
        public const bool DefaultDropBothHands = true;
        public const int DefaultChancePercent = 100;
        public const bool DefaultStayDisarmed = false;

        /// <summary>Even dice between the three reactions (user 2026-11-22: "je nach Würfeln").</summary>
        public const int DefaultShockChance = 50;

        public const int DefaultBackOffChance = 50;
        public const int DefaultCowerChance = 50;

        /// <summary>Seconds of each reaction; then the enemy goes for a weapon (guesses for the headset).</summary>
        public static readonly SliderSpec ShockTime = new SliderSpec(ShockTimeName, 1f, 15f, 1f, 2f);

        public static readonly SliderSpec BackOffTime = new SliderSpec(BackOffTimeName, 1f, 15f, 1f, 2f);
        public static readonly SliderSpec CowerTime = new SliderSpec(CowerTimeName, 1f, 15f, 1f, 5f);

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

        /// <summary>
        /// The game's own brain the disarmed NPC gets while it stands in shock (and for good with Stays Permanent). Seen in BasHarness runs
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

    public enum DisarmReaction
    {
        /// <summary>No chance set: the enemy goes for a weapon at once.</summary>
        None,

        /// <summary>It freezes for a moment (the game's passive HumanDummy brain).</summary>
        Shock,

        /// <summary>It steps back away from the player, facing the player (the game's BrainModuleMove).</summary>
        BackOff,

        /// <summary>It cowers in fear (the game's BrainModuleFear.Panic: a scream and the HumanCower animation).</summary>
        Cower,
    }

    /// <summary>The dice for a disarmed enemy's reaction (user 2026-11-22) and how long it lasts.</summary>
    public static class DisarmReactions
    {
        /// <summary>Walking speed while backing off, as a share of the enemy's forward speed (a guess for the headset).</summary>
        public const float BackOffSpeedRatio = 0.5f;

        /// <summary>Longest wait (s) for a running stagger to end before the reaction starts anyway (a guess).</summary>
        public const float MaxStaggerWait = 3f;

        /// <summary>
        /// A reaction starts once the enemy is no longer staggered, or after <see cref="MaxStaggerWait"/>: a stagger push
        /// ends the game's cowering at once (BrainModuleFear.CreaturePushed), and a move would cut the stagger animation.
        /// </summary>
        public static bool ReadyToReact(bool staggered, float waited) => !staggered || waited >= MaxStaggerWait;

        /// <summary>One of the reactions by its chance against the others (roll 0..1); None when every chance is 0.</summary>
        public static DisarmReaction Pick(double roll, int shock, int backOff, int cower)
        {
            var s = Math.Max(0, shock);
            var b = Math.Max(0, backOff);
            var c = Math.Max(0, cower);
            var total = s + b + c;
            if (total == 0)
            {
                return DisarmReaction.None;
            }
            var at = Math.Min(0.999999, Math.Max(0.0, roll)) * total;
            if (at < s)
            {
                return DisarmReaction.Shock;
            }
            return at < s + b ? DisarmReaction.BackOff : DisarmReaction.Cower;
        }

        public static float Seconds(DisarmReaction reaction, float shock, float backOff, float cower)
        {
            switch (reaction)
            {
                case DisarmReaction.Shock: return shock;
                case DisarmReaction.BackOff: return backOff;
                case DisarmReaction.Cower: return cower;
                default: return 0f;
            }
        }
    }
}
