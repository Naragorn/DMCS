namespace DMCS.Core
{
    /// <summary>Who dealt a hit, as far as Charge Stagger cares.</summary>
    public enum HitSource
    {
        /// <summary>Not the player (another NPC, the environment, nothing).</summary>
        NotPlayer,

        /// <summary>A weapon/item held in the player's hand.</summary>
        HeldWeapon,

        /// <summary>The player's own body (punch, kick).</summary>
        PlayerBody,

        /// <summary>An item the player threw (last held by the player, no hand on it now).</summary>
        PlayerThrown,

        /// <summary>A spell the player cast.</summary>
        PlayerSpell,
    }

    public enum ChargeDecision
    {
        Stagger,
        KnockDown,
        Disabled,
        NotFinalPhase,
        NotByPlayer,
        RangedNotCounted,
        TargetIsPlayer,
        TargetDead,
        TooSlow,
        SwingTooWeak,
        ChanceFailed,
        AlreadyStaggered,
    }

    /// <summary>The facts and settings that decide one hit (plain values, read from the game by the caller).</summary>
    public readonly struct ChargeHit
    {
        public ChargeHit(
            bool enabled, bool finalPhase, HitSource source, bool rangedCounts, bool targetIsPlayer, bool targetKilled,
            bool alreadyStaggered, bool secondKnocksDown, float peakSpeed, float threshold, float impactSpeed,
            float minImpactSpeed, float roll, float chance)
        {
            Enabled = enabled;
            FinalPhase = finalPhase;
            Source = source;
            RangedCounts = rangedCounts;
            TargetIsPlayer = targetIsPlayer;
            TargetKilled = targetKilled;
            AlreadyStaggered = alreadyStaggered;
            SecondKnocksDown = secondKnocksDown;
            PeakSpeed = peakSpeed;
            Threshold = threshold;
            ImpactSpeed = impactSpeed;
            MinImpactSpeed = minImpactSpeed;
            Roll = roll;
            Chance = chance;
        }

        public bool Enabled { get; }

        /// <summary>The hit event's OnEnd: damage has been applied and the target survived it.</summary>
        public bool FinalPhase { get; }

        public HitSource Source { get; }

        /// <summary>Thrown weapons and spells count (Mod Menu).</summary>
        public bool RangedCounts { get; }

        public bool TargetIsPlayer { get; }
        public bool TargetKilled { get; }

        /// <summary>The NPC is already staggered or down (DMCS window or the game's own stagger).</summary>
        public bool AlreadyStaggered { get; }

        /// <summary>A charge hit on an already staggered NPC knocks it down (Mod Menu).</summary>
        public bool SecondKnocksDown { get; }

        /// <summary>The player's top running speed in the speed window (m/s).</summary>
        public float PeakSpeed { get; }

        public float Threshold { get; }

        /// <summary>How fast the weapon/fist/projectile hit (|CollisionInstance.impactVelocity|, m/s).</summary>
        public float ImpactSpeed { get; }

        public float MinImpactSpeed { get; }

        /// <summary>Random number in [0, 1).</summary>
        public float Roll { get; }

        /// <summary>0..1.</summary>
        public float Chance { get; }
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
            if (hit.TargetIsPlayer)
            {
                return ChargeDecision.TargetIsPlayer;
            }
            if (hit.TargetKilled)
            {
                return ChargeDecision.TargetDead;
            }
            if (IsRanged(hit.Source) && !hit.RangedCounts)
            {
                return ChargeDecision.RangedNotCounted;
            }
            if (hit.PeakSpeed < hit.Threshold)
            {
                return ChargeDecision.TooSlow;
            }
            if (hit.ImpactSpeed < hit.MinImpactSpeed)
            {
                return ChargeDecision.SwingTooWeak;
            }
            if (!(hit.Roll < hit.Chance))
            {
                return ChargeDecision.ChanceFailed;
            }
            if (hit.AlreadyStaggered)
            {
                return hit.SecondKnocksDown ? ChargeDecision.KnockDown : ChargeDecision.AlreadyStaggered;
            }
            return ChargeDecision.Stagger;
        }

        public static bool IsRanged(HitSource source)
        {
            return source == HitSource.PlayerThrown || source == HitSource.PlayerSpell;
        }

        /// <summary>Decisions about hits that are not a player hit on a living NPC: not worth a debug line.</summary>
        public static bool IsQuiet(ChargeDecision decision)
        {
            switch (decision)
            {
                case ChargeDecision.Disabled:
                case ChargeDecision.NotFinalPhase:
                case ChargeDecision.NotByPlayer:
                case ChargeDecision.TargetIsPlayer:
                case ChargeDecision.TargetDead:
                    return true;
                default:
                    return false;
            }
        }
    }
}
