using System;
using System.Collections.Generic;

namespace DMCS.Core
{
    public enum BodyHitDecision
    {
        Drop,
        Disabled,
        NotByPlayer,
        TargetIsPlayer,
        TargetDead,

        /// <summary>A hand (or forearm, if it counts) hit: Disarm Strike's own rule decides.</summary>
        DisarmPart,

        NotStaggered,
        NothingToDrop,
        ChanceFailed,
    }

    /// <summary>The facts and settings that decide one body hit (plain values, read from the game by the caller).</summary>
    public readonly struct BodyHit
    {
        public BodyHit(
            bool enabled, bool modeOn, HitSource source, bool targetIsPlayer, bool targetKilled, HitPart part,
            bool includeForearm, bool staggered, bool holdsDroppable, int earlierHits, float roll, float baseChance,
            float perExtraHit)
        {
            Enabled = enabled;
            ModeOn = modeOn;
            Source = source;
            TargetIsPlayer = targetIsPlayer;
            TargetKilled = targetKilled;
            Part = part;
            IncludeForearm = includeForearm;
            Staggered = staggered;
            HoldsDroppable = holdsDroppable;
            EarlierHits = earlierHits;
            Roll = roll;
            BaseChance = baseChance;
            PerExtraHit = perExtraHit;
        }

        /// <summary>Disarm Strike is on.</summary>
        public bool Enabled { get; }

        /// <summary>"Body Hit Drop" is on.</summary>
        public bool ModeOn { get; }

        public HitSource Source { get; }
        public bool TargetIsPlayer { get; }
        public bool TargetKilled { get; }
        public HitPart Part { get; }
        public bool IncludeForearm { get; }

        /// <summary>Staggered before this hit (as Disarm Strike judges it).</summary>
        public bool Staggered { get; }

        /// <summary>A hand holds an item it may drop (shields only when allowed).</summary>
        public bool HoldsDroppable { get; }

        /// <summary>Body hits in this stagger that did not drop the weapon.</summary>
        public int EarlierHits { get; }

        public float Roll { get; }
        public float BaseChance { get; }
        public float PerExtraHit { get; }
    }

    public readonly struct BodyHitResult
    {
        public BodyHitResult(BodyHitDecision decision, float chance)
        {
            Decision = decision;
            Chance = chance;
        }

        public BodyHitDecision Decision { get; }

        /// <summary>The chance this hit rolled against (0 when it did not roll).</summary>
        public float Chance { get; }
    }

    /// <summary>
    /// Body Hit Drop: a player hit anywhere but the hand on a staggered NPC makes it drop its weapon with a chance;
    /// every earlier such hit in the same stagger raises the chance.
    /// </summary>
    public static class BodyHitDropRule
    {
        public static BodyHitResult Decide(BodyHit h)
        {
            if (!h.Enabled || !h.ModeOn)
            {
                return Refuse(BodyHitDecision.Disabled);
            }
            if (h.Source == HitSource.NotPlayer)
            {
                return Refuse(BodyHitDecision.NotByPlayer);
            }
            if (h.TargetIsPlayer)
            {
                return Refuse(BodyHitDecision.TargetIsPlayer);
            }
            if (h.TargetKilled)
            {
                return Refuse(BodyHitDecision.TargetDead);
            }
            if (DisarmRule.IsDisarmPart(h.Part, h.IncludeForearm))
            {
                return Refuse(BodyHitDecision.DisarmPart);
            }
            if (!h.Staggered)
            {
                return Refuse(BodyHitDecision.NotStaggered);
            }
            if (!h.HoldsDroppable)
            {
                return Refuse(BodyHitDecision.NothingToDrop);
            }
            var chance = Chance(h.BaseChance, h.PerExtraHit, h.EarlierHits);
            return new BodyHitResult(h.Roll < chance ? BodyHitDecision.Drop : BodyHitDecision.ChanceFailed, chance);
        }

        /// <summary>The first-hit chance for this NPC: the highest of the base chance and the chances of the types it belongs to.</summary>
        public static float NpcChance(float baseChance, bool isFemale, float femaleChance, bool isSmaller, float smallerChance)
        {
            var chance = baseChance;
            if (isFemale)
            {
                chance = Math.Max(chance, femaleChance);
            }
            if (isSmaller)
            {
                chance = Math.Max(chance, smallerChance);
            }
            return chance;
        }

        /// <summary>Base chance plus the bonus for each earlier hit, at most certain.</summary>
        public static float Chance(float baseChance, float perExtraHit, int earlierHits)
        {
            return Math.Min(1f, baseChance + (perExtraHit * Math.Max(0, earlierHits)));
        }

        /// <summary>Only a hit that rolled and missed raises the next hit's chance.</summary>
        public static bool CountsAsEarlierHit(BodyHitDecision decision)
        {
            return decision == BodyHitDecision.ChanceFailed;
        }

        /// <summary>Decisions not about a player body hit on a staggered living NPC: not worth a debug line.</summary>
        public static bool IsQuiet(BodyHitDecision decision)
        {
            switch (decision)
            {
                case BodyHitDecision.Disabled:
                case BodyHitDecision.NotByPlayer:
                case BodyHitDecision.TargetIsPlayer:
                case BodyHitDecision.TargetDead:
                case BodyHitDecision.DisarmPart:
                case BodyHitDecision.NotStaggered:
                    return true;
                default:
                    return false;
            }
        }

        private static BodyHitResult Refuse(BodyHitDecision decision)
        {
            return new BodyHitResult(decision, 0f);
        }
    }

    /// <summary>Per NPC: the body hits in its current stagger that did not drop the weapon.</summary>
    public sealed class StaggerHitCounts<TKey>
    {
        private readonly Dictionary<TKey, int> _hits = new Dictionary<TKey, int>();

        public int Count => _hits.Count;

        public void Add(TKey key)
        {
            _hits[key] = Get(key) + 1;
        }

        public int Get(TKey key)
        {
            return _hits.TryGetValue(key, out var hits) ? hits : 0;
        }

        /// <summary>Forgets the count of every NPC no longer staggered (<paramref name="stillStaggered"/> false).</summary>
        public void EndStaggers(Func<TKey, bool> stillStaggered)
        {
            var ended = new List<TKey>();
            foreach (var key in _hits.Keys)
            {
                if (!stillStaggered(key))
                {
                    ended.Add(key);
                }
            }
            foreach (var key in ended)
            {
                _hits.Remove(key);
            }
        }

        public void Forget(TKey key)
        {
            _hits.Remove(key);
        }

        public void Clear()
        {
            _hits.Clear();
        }
    }

    /// <summary>"Smaller" for the size filter: the NPC is shorter than the player by at least the margin (heights in m).</summary>
    public static class SizeFilter
    {
        public static bool IsSmaller(float npcHeight, float playerHeight, float marginMeters)
        {
            // Half a millimetre of slack keeps float rounding from deciding a tie (1.8 - 0.1 vs 1.7).
            return npcHeight > 0f && playerHeight > 0f && playerHeight - npcHeight > marginMeters + 0.0005f;
        }
    }

    /// <summary>A hand holding a shield keeps it unless switched on (as the game's own hand-hit disarm keeps shields).</summary>
    public static class DisarmShield
    {
        public static bool Keeps(bool isShield, bool shieldsCanBeDisarmed)
        {
            return isShield && !shieldsCanBeDisarmed;
        }
    }
}
