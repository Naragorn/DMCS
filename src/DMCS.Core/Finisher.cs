namespace DMCS.Core
{
    public enum FinisherDecision
    {
        Disabled,

        /// <summary>Not a weapon in the player's hand (a punch, a kick, a throw, a spell, not the player).</summary>
        NotAHeldWeapon,
        TargetIsPlayer,
        TargetDead,

        /// <summary>The enemy stands (its ragdoll is not destabilized).</summary>
        NotDowned,

        /// <summary>A kneeling enemy belongs to the adrenaline execution.</summary>
        Kneeling,
        NotTorso,

        /// <summary>A cut or a blunt hit, not a stab.</summary>
        NotAStab,
        Finish,
    }

    /// <summary>The facts of one player hit for the finisher (plain values, read by the caller).</summary>
    public readonly struct FinisherHit
    {
        public FinisherHit(bool enabled, HitSource source, bool targetIsPlayer, bool targetDead, bool downed, bool kneeling, bool torso, bool stab)
        {
            Enabled = enabled;
            Source = source;
            TargetIsPlayer = targetIsPlayer;
            TargetDead = targetDead;
            Downed = downed;
            Kneeling = kneeling;
            Torso = torso;
            Stab = stab;
        }

        public bool Enabled { get; }
        public HitSource Source { get; }
        public bool TargetIsPlayer { get; }
        public bool TargetDead { get; }
        public bool Downed { get; }
        public bool Kneeling { get; }
        public bool Torso { get; }

        /// <summary>The game marks a stab by penetration; a Pierce hit counts too (as the chest-stab fatality).</summary>
        public bool Stab { get; }
    }

    /// <summary>
    /// Finisher on a downed enemy (user 2026-11-23, default on; Dark Messiah: a knocked-down enemy is finished with a stab
    /// into the chest, no adrenaline needed): a stab with a held weapon into the torso of a living enemy on the ground kills it.
    /// </summary>
    public static class FinisherRule
    {
        public static FinisherDecision Decide(FinisherHit h)
        {
            if (!h.Enabled)
            {
                return FinisherDecision.Disabled;
            }
            if (h.Source != HitSource.HeldWeapon)
            {
                return FinisherDecision.NotAHeldWeapon;
            }
            if (h.TargetIsPlayer)
            {
                return FinisherDecision.TargetIsPlayer;
            }
            if (h.TargetDead)
            {
                return FinisherDecision.TargetDead;
            }
            if (!h.Downed)
            {
                return FinisherDecision.NotDowned;
            }
            if (h.Kneeling)
            {
                return FinisherDecision.Kneeling;
            }
            if (!h.Torso)
            {
                return FinisherDecision.NotTorso;
            }
            return h.Stab ? FinisherDecision.Finish : FinisherDecision.NotAStab;
        }
    }

    public static class FinisherSettings
    {
        public const string EnabledName = DmcsCategories.Finisher + " Enabled";
        public const bool DefaultEnabled = true;
    }
}
