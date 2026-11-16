using System;

namespace DMCS.Core
{
    /// <summary>
    /// The stagger levels the game itself has (BrainModuleHitReaction.PushBehaviour.Effect:
    /// StaggerLight, StaggerMedium, StaggerFull, Destabilize). Knock Down = Destabilize: the NPC falls.
    /// </summary>
    public enum StaggerStrength
    {
        Light,
        Medium,
        Full,
        KnockDown,
    }

    public static class StaggerStrengths
    {
        /// <summary>Mod Menu names, weak to strong.</summary>
        public static readonly string[] Names = { "Light", "Medium", "Full", "Knock Down" };

        public static string NameOf(StaggerStrength strength)
        {
            return Names[(int)strength];
        }

        /// <summary>Menu name to strength; anything unknown gives the Charge Stagger default.</summary>
        public static StaggerStrength Parse(string name)
        {
            var index = Array.IndexOf(Names, name);
            if (index < 0)
            {
                index = Array.IndexOf(Names, ChargeStaggerSettings.DefaultStrength);
            }
            return (StaggerStrength)index;
        }

        /// <summary>Knock Down leaves the NPC on the ground; DMCS keeps it there for the stagger duration.</summary>
        public static bool KnocksDown(StaggerStrength strength)
        {
            return strength == StaggerStrength.KnockDown;
        }
    }
}
