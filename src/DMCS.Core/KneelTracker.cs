using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    public enum KneelReason
    {
        None,

        /// <summary>No player hit landed for the set seconds since the window opened or the last hit.</summary>
        NoHit,

        /// <summary>Only light hits landed: the set number of them.</summary>
        LightHits,

        /// <summary>The player won a Clash of Might and the kneel chance came up.</summary>
        ClashWin,
    }

    /// <summary>
    /// Step 4c: while the adrenaline window is open, the enemy goes down on its knees when no hit lands for a while or
    /// when only light hits land. Which enemy: the one the player hit last (the caller falls back to the nearest attacker).
    /// Times in seconds on the caller's clock.
    /// </summary>
    public sealed class KneelTracker<TKey>
    {
        private bool _open;
        private float _lastHitAt;
        private int _lightHits;
        private bool _hasTarget;

        public TKey LastHit { get; private set; }

        public bool HasLastHit => _hasTarget;

        /// <summary>Any player hit on an enemy (window open or not) makes it the one that kneels.</summary>
        public void PlayerHit(TKey target, float now, bool light)
        {
            LastHit = target;
            _hasTarget = true;
            if (!_open)
            {
                return;
            }
            if (light)
            {
                _lightHits++;
            }
            else
            {
                _lastHitAt = now;
            }
        }

        public void WindowOpened(float now)
        {
            _open = true;
            _lastHitAt = now;
            _lightHits = 0;
        }

        public void WindowClosed()
        {
            _open = false;
            _lightHits = 0;
        }

        /// <summary>The enemy hit last is gone (dead, despawned): no one to kneel until the next hit.</summary>
        public void Forget(TKey target)
        {
            if (_hasTarget && EqualityComparer<TKey>.Default.Equals(LastHit, target))
            {
                _hasTarget = false;
                LastHit = default(TKey);
            }
        }

        public KneelReason Check(float now, bool enabled, float noHitSeconds, int lightHitsNeeded)
        {
            if (!enabled || !_open)
            {
                return KneelReason.None;
            }
            if (lightHitsNeeded > 0 && _lightHits >= lightHitsNeeded)
            {
                return KneelReason.LightHits;
            }
            return now - _lastHitAt >= noHitSeconds ? KneelReason.NoHit : KneelReason.None;
        }
    }

    public enum KneelEnd
    {
        StandsUp,
        Dies,
    }

    public static class KneelOutcome
    {
        /// <summary>The execution window ran out: the enemy stands up, or dies with the set chance.</summary>
        public static KneelEnd AfterWindow(float roll, float deathChance) => roll < deathChance ? KneelEnd.Dies : KneelEnd.StandsUp;
    }

    /// <summary>A creature that might kneel: how far it is from the player and whether it fights the player.</summary>
    public readonly struct KneelCandidate
    {
        public KneelCandidate(int id, float distance, bool alive, bool attacksPlayer)
        {
            Id = id;
            Distance = distance;
            Alive = alive;
            AttacksPlayer = attacksPlayer;
        }

        public int Id { get; }
        public float Distance { get; }
        public bool Alive { get; }
        public bool AttacksPlayer { get; }
    }

    /// <summary>Which enemy kneels: only one near the player (headset 2026-11-22: far enemies knelt too).</summary>
    public static class KneelTargetRule
    {
        /// <summary>
        /// The enemy hit last, if alive and within <paramref name="maxDistance"/> m; else the nearest living enemy that
        /// attacks the player within it; else nobody (null).
        /// </summary>
        public static int? Choose(int? lastHit, IEnumerable<KneelCandidate> candidates, float maxDistance)
        {
            if (candidates == null)
            {
                return null;
            }
            var near = candidates.Where(c => c.Alive && c.Distance <= maxDistance).ToList();
            if (lastHit.HasValue && near.Any(c => c.Id == lastHit.Value))
            {
                return lastHit.Value;
            }
            var attacker = near.Where(c => c.AttacksPlayer).OrderBy(c => c.Distance).Select(c => (int?)c.Id).FirstOrDefault();
            return attacker;
        }
    }
}
