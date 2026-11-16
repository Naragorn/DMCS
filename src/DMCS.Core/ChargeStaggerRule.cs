namespace DMCS.Core
{
    /// <summary>Who dealt a hit, as far as Charge Stagger cares.</summary>
    public enum HitSource
    {
        /// <summary>Not the player (another NPC, the environment, nothing).</summary>
        NotPlayer,

        /// <summary>The player, but not in melee (thrown item, spell).</summary>
        PlayerNotMelee,

        /// <summary>A weapon/item held in the player's hand.</summary>
        HeldWeapon,

        /// <summary>The player's own body (punch, kick).</summary>
        PlayerBody,
    }

    public enum ChargeDecision
    {
        Stagger,
        Disabled,
        NotFinalPhase,
        NotByPlayer,
        NotMelee,
        TargetIsPlayer,
        TargetDead,
        AlreadyStaggered,
        TooSlow,
    }

    /// <summary>The facts about one hit that decide a charge stagger (plain values, read from the game by the caller).</summary>
    public readonly struct ChargeHit
    {
        public ChargeHit(bool enabled, bool finalPhase, HitSource source, bool targetIsPlayer, bool targetKilled, bool alreadyStaggered, float peakSpeed, float threshold)
        {
            Enabled = enabled;
            FinalPhase = finalPhase;
            Source = source;
            TargetIsPlayer = targetIsPlayer;
            TargetKilled = targetKilled;
            AlreadyStaggered = alreadyStaggered;
            PeakSpeed = peakSpeed;
            Threshold = threshold;
        }

        public bool Enabled { get; }

        /// <summary>The hit event's OnEnd: damage has been applied and the target survived it.</summary>
        public bool FinalPhase { get; }

        public HitSource Source { get; }
        public bool TargetIsPlayer { get; }
        public bool TargetKilled { get; }

        /// <summary>Still inside the stagger window of an earlier charge hit (one charge, one stagger).</summary>
        public bool AlreadyStaggered { get; }

        public float PeakSpeed { get; }
        public float Threshold { get; }
    }

    public static class ChargeStaggerRule
    {
        public static ChargeDecision Decide(ChargeHit hit)
        {
            if (!hit.Enabled)
            {
                return ChargeDecision.Disabled;
            }
            if (!hit.FinalPhase)
            {
                return ChargeDecision.NotFinalPhase;
            }
            if (hit.Source == HitSource.NotPlayer)
            {
                return ChargeDecision.NotByPlayer;
            }
            if (hit.Source == HitSource.PlayerNotMelee)
            {
                return ChargeDecision.NotMelee;
            }
            if (hit.TargetIsPlayer)
            {
                return ChargeDecision.TargetIsPlayer;
            }
            if (hit.TargetKilled)
            {
                return ChargeDecision.TargetDead;
            }
            if (hit.AlreadyStaggered)
            {
                return ChargeDecision.AlreadyStaggered;
            }
            return hit.PeakSpeed >= hit.Threshold ? ChargeDecision.Stagger : ChargeDecision.TooSlow;
        }

        /// <summary>Decisions about hits that are not a player melee hit on a living NPC: not worth a debug line.</summary>
        public static bool IsQuiet(ChargeDecision decision)
        {
            return decision != ChargeDecision.Stagger && decision != ChargeDecision.TooSlow && decision != ChargeDecision.AlreadyStaggered;
        }
    }
}
