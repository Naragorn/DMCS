namespace DMCS.Core
{
    /// <summary>The hit body part, as far as Disarm Strike cares (mapped from RagdollPart.Type by the caller).</summary>
    public enum HitPart
    {
        Other,
        LeftHand,
        RightHand,

        /// <summary>RagdollPart.Type.LeftArm (the game has no separate forearm type).</summary>
        LeftArm,

        RightArm,
    }

    public enum DisarmDecision
    {
        Disarm,
        Disabled,
        NotFinalPhase,
        NotByPlayer,
        TargetIsPlayer,
        TargetDead,
        WrongPart,
        NotStaggered,
        NothingHeld,
        ChanceFailed,
    }

    /// <summary>The facts and settings that decide one hit (plain values, read from the game by the caller).</summary>
    public readonly struct DisarmHit
    {
        public DisarmHit(
            bool enabled, bool finalPhase, HitSource source, bool targetIsPlayer, bool targetKilled, HitPart part,
            bool includeForearm, bool requireStagger, bool staggered, bool holdsSomething, float roll, float chance)
        {
            Enabled = enabled;
            FinalPhase = finalPhase;
            Source = source;
            TargetIsPlayer = targetIsPlayer;
            TargetKilled = targetKilled;
            Part = part;
            IncludeForearm = includeForearm;
            RequireStagger = requireStagger;
            Staggered = staggered;
            HoldsSomething = holdsSomething;
            Roll = roll;
            Chance = chance;
        }

        public bool Enabled { get; }
        public bool FinalPhase { get; }
        public HitSource Source { get; }
        public bool TargetIsPlayer { get; }
        public bool TargetKilled { get; }
        public HitPart Part { get; }
        public bool IncludeForearm { get; }
        public bool RequireStagger { get; }

        /// <summary>Charge Stagger window, the game's own stagger, or lying on the ground.</summary>
        public bool Staggered { get; }

        /// <summary>A hand that would drop something holds an item.</summary>
        public bool HoldsSomething { get; }

        public float Roll { get; }
        public float Chance { get; }
    }

    public static class DisarmRule
    {
        public static DisarmDecision Decide(DisarmHit hit)
        {
            if (!hit.Enabled)
            {
                return DisarmDecision.Disabled;
            }
            if (!hit.FinalPhase)
            {
                return DisarmDecision.NotFinalPhase;
            }
            if (hit.Source == HitSource.NotPlayer)
            {
                return DisarmDecision.NotByPlayer;
            }
            if (hit.TargetIsPlayer)
            {
                return DisarmDecision.TargetIsPlayer;
            }
            if (hit.TargetKilled)
            {
                return DisarmDecision.TargetDead;
            }
            if (!IsDisarmPart(hit.Part, hit.IncludeForearm))
            {
                return DisarmDecision.WrongPart;
            }
            if (hit.RequireStagger && !hit.Staggered)
            {
                return DisarmDecision.NotStaggered;
            }
            if (!hit.HoldsSomething)
            {
                return DisarmDecision.NothingHeld;
            }
            return hit.Roll < hit.Chance ? DisarmDecision.Disarm : DisarmDecision.ChanceFailed;
        }

        public static bool IsDisarmPart(HitPart part, bool includeForearm)
        {
            switch (part)
            {
                case HitPart.LeftHand:
                case HitPart.RightHand:
                    return true;
                case HitPart.LeftArm:
                case HitPart.RightArm:
                    return includeForearm;
                default:
                    return false;
            }
        }

        /// <summary>Decisions about hits that are not a player hit on a living NPC: not worth a debug line.</summary>
        public static bool IsQuiet(DisarmDecision decision)
        {
            switch (decision)
            {
                case DisarmDecision.Disabled:
                case DisarmDecision.NotFinalPhase:
                case DisarmDecision.NotByPlayer:
                case DisarmDecision.TargetIsPlayer:
                case DisarmDecision.TargetDead:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>Which of the NPC's hands let go.</summary>
    public readonly struct DisarmHands
    {
        public DisarmHands(bool left, bool right)
        {
            Left = left;
            Right = right;
        }

        public bool Left { get; }
        public bool Right { get; }

        /// <summary>Hands to keep empty after another disarm: those of earlier disarms still running, plus these.</summary>
        public DisarmHands With(DisarmHands other) => new DisarmHands(Left || other.Left, Right || other.Right);

        /// <summary>The hit side; both hands when <paramref name="dropBoth"/> (two-handed and dual-wielded weapons).</summary>
        public static DisarmHands ToRelease(HitPart part, bool dropBoth)
        {
            var left = part == HitPart.LeftHand || part == HitPart.LeftArm;
            var right = part == HitPart.RightHand || part == HitPart.RightArm;
            if (!left && !right)
            {
                return new DisarmHands(false, false);
            }
            return dropBoth ? new DisarmHands(true, true) : new DisarmHands(left, right);
        }
    }
}
