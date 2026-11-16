using System;
using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    public enum CriticalDecision
    {
        Critical,
        Disabled,
        NotReady,
        NotByPlayer,
        TargetIsPlayer,
        TargetDead,

        /// <summary>A thrown weapon or spell while "Critical Thrown And Spells" is off.</summary>
        Ranged,

        /// <summary>Slower than the hard-swing speed while "Critical Needs Hard Swing" is on.</summary>
        TooLight,
    }

    /// <summary>The facts and settings that decide whether a hit is the critical (plain values, read by the caller).</summary>
    public readonly struct CriticalHit
    {
        public CriticalHit(
            bool enabled, bool ready, HitSource source, bool rangedCounts, bool targetIsPlayer, bool targetDead,
            bool needsHardSwing, float impactSpeed, float minSwingSpeed)
        {
            Enabled = enabled;
            Ready = ready;
            Source = source;
            RangedCounts = rangedCounts;
            TargetIsPlayer = targetIsPlayer;
            TargetDead = targetDead;
            NeedsHardSwing = needsHardSwing;
            ImpactSpeed = impactSpeed;
            MinSwingSpeed = minSwingSpeed;
        }

        public bool Enabled { get; }

        /// <summary>The meter is full (window open).</summary>
        public bool Ready { get; }

        public HitSource Source { get; }
        public bool RangedCounts { get; }
        public bool TargetIsPlayer { get; }

        /// <summary>Dead before this hit.</summary>
        public bool TargetDead { get; }

        public bool NeedsHardSwing { get; }
        public float ImpactSpeed { get; }
        public float MinSwingSpeed { get; }
    }

    /// <summary>The critical: the next qualifying player hit while the adrenaline window is open.</summary>
    public static class CriticalRule
    {
        public static CriticalDecision Decide(CriticalHit h)
        {
            if (!h.Enabled)
            {
                return CriticalDecision.Disabled;
            }
            if (h.Source == HitSource.NotPlayer)
            {
                return CriticalDecision.NotByPlayer;
            }
            if (h.TargetIsPlayer)
            {
                return CriticalDecision.TargetIsPlayer;
            }
            if (h.TargetDead)
            {
                return CriticalDecision.TargetDead;
            }
            if (!h.Ready)
            {
                return CriticalDecision.NotReady;
            }
            if (!h.RangedCounts && (h.Source == HitSource.PlayerThrown || h.Source == HitSource.PlayerSpell))
            {
                return CriticalDecision.Ranged;
            }
            if (h.NeedsHardSwing && h.ImpactSpeed < h.MinSwingSpeed)
            {
                return CriticalDecision.TooLight;
            }
            return CriticalDecision.Critical;
        }

        /// <summary>
        /// The chest-stab fatality: a stab into the torso. The game marks a stab by penetration
        /// (DamageStruct.penetration != None); a Pierce hit counts too, as its blade went in.
        /// </summary>
        public static bool IsChestStab(bool hitTorso, bool penetrating, bool pierceDamage)
        {
            return hitTorso && (penetrating || pierceDamage);
        }
    }

    /// <summary>The death-animation direction the game picks from the hit (AnimationData.Clip.Direction).</summary>
    public enum DeathDirection
    {
        Forward,
        Backward,
        Left,
        Right,
    }

    /// <summary>
    /// Which kneeling death clips (by catalog address) play for each direction, so the chest-stab fatality always kneels.
    /// The game's HumanStabDeaths holds Death_KneelBackward, Death_KneelMid (moveDirection Backward) and
    /// Death_KneelForward, …2, …3 (Forward); Left and Right have none, so they get the middle one.
    /// </summary>
    public static class KneelClips
    {
        public static bool IsKneel(string address) =>
            address != null && address.IndexOf("Kneel", StringComparison.OrdinalIgnoreCase) >= 0;

        public static List<string> For(DeathDirection direction, IEnumerable<string> addresses)
        {
            var kneels = (addresses ?? Enumerable.Empty<string>()).Where(IsKneel).Distinct().ToList();
            List<string> fitting;
            switch (direction)
            {
                case DeathDirection.Forward:
                    fitting = kneels.Where(a => Contains(a, "KneelForward")).ToList();
                    break;
                case DeathDirection.Backward:
                    fitting = kneels.Where(a => Contains(a, "KneelBackward") || Contains(a, "KneelMid")).ToList();
                    break;
                default:
                    fitting = kneels.Where(a => Contains(a, "KneelMid")).ToList();
                    break;
            }
            return fitting.Count > 0 ? fitting : kneels;
        }

        private static bool Contains(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Which slow-motion the critical uses.</summary>
    public static class CriticalTimeModes
    {
        public const string Hyperfocus = "Hyperfocus";
        public const string SlowTime = "Slow Time";

        public static readonly string[] Names = { Hyperfocus, SlowTime };

        /// <summary>Hyperfocus keeps the player near normal speed; Slow Time slows the player too.</summary>
        public static bool BuffsPlayer(string mode) => mode != SlowTime;

        /// <summary>
        /// Player buffs for a world slowed to <paramref name="scale"/>, after the game's Hyperfocus skill
        /// (catalog Skill_HyperFocus at timeScale 0.1: joints ×10, speed ×20, drag ×10), scaled with 1/scale.
        /// </summary>
        public static (float joint, float speed, float drag) Buffs(float scale)
        {
            var s = Math.Max(0.01f, scale);
            return (1f / s, 2f / s, 1f / s);
        }
    }
}
