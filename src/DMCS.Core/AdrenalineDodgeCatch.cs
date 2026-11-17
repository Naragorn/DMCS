using System;
using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    public enum DodgeResult
    {
        /// <summary>No open attack from this NPC (it never reached its Attack stage, was cancelled or already ended).</summary>
        NotTracked,

        /// <summary>The attack hit the player.</summary>
        Hit,

        /// <summary>The player blocked or parried it.</summary>
        Parried,

        /// <summary>It missed, but the player did not move fast enough to call it a dodge.</summary>
        TooSlow,

        Dodged,
    }

    /// <summary>
    /// Step 4e: an NPC melee attack on the player that reached its Attack stage and ended (not cancelled) without
    /// hitting the player and without a block or parry, while the player moved fast, is a dodge. The game has no dodge
    /// event; this is an approximation. Keyed by the attacker (one attack per NPC at a time, as BrainModuleMelee runs).
    /// </summary>
    public sealed class DodgeTracker<TId>
    {
        private sealed class Attack
        {
            public float StartedAt;
            public DodgeResult Spoiled = DodgeResult.Dodged;
        }

        private readonly Dictionary<TId, Attack> _open = new Dictionary<TId, Attack>();

        /// <summary>The attack reached AttackStage.Attack (a new attack replaces an earlier one from the same NPC).</summary>
        public void AttackStarted(TId attacker, float now) => _open[attacker] = new Attack { StartedAt = now };

        public bool TryStartedAt(TId attacker, out float startedAt)
        {
            var found = _open.TryGetValue(attacker, out var attack);
            startedAt = found ? attack.StartedAt : 0f;
            return found;
        }

        public void PlayerHitBy(TId attacker)
        {
            if (_open.TryGetValue(attacker, out var attack))
            {
                attack.Spoiled = DodgeResult.Hit;
            }
        }

        /// <summary>A hit on the player whose attacker cannot be read: no open attack may count as dodged.</summary>
        public void PlayerHitByUnknown()
        {
            foreach (var attack in _open.Values)
            {
                attack.Spoiled = DodgeResult.Hit;
            }
        }

        public void Parried(TId attacker)
        {
            if (_open.TryGetValue(attacker, out var attack) && attack.Spoiled == DodgeResult.Dodged)
            {
                attack.Spoiled = DodgeResult.Parried;
            }
        }

        public void AttackCancelled(TId attacker) => _open.Remove(attacker);

        /// <summary>The attack ended: its result. <paramref name="peakSpeed"/> is the player's fastest speed (m/s) since it started.</summary>
        public DodgeResult AttackEnded(TId attacker, float peakSpeed, float minSpeed)
        {
            if (!_open.TryGetValue(attacker, out var attack))
            {
                return DodgeResult.NotTracked;
            }
            _open.Remove(attacker);
            if (attack.Spoiled != DodgeResult.Dodged)
            {
                return attack.Spoiled;
            }
            return peakSpeed >= minSpeed ? DodgeResult.Dodged : DodgeResult.TooSlow;
        }

        public void Clear() => _open.Clear();
    }

    /// <summary>
    /// The player's horizontal head speed for the dodge, measured over a short window: the rig moves in physics steps,
    /// so per-frame speeds jump between 0 and several times the true speed. A jump of more than <see cref="MaxStep"/>
    /// between two frames is a teleport (turn, respawn) and starts the measurement over.
    /// </summary>
    public sealed class HeadSpeedMeter
    {
        /// <summary>Metres between two frames above which the head was teleported (6 m/s sprint at 10 fps = 0.6 m).</summary>
        public const float MaxStep = 1f;

        private readonly float _window;
        private readonly List<(float T, float X, float Z)> _samples = new List<(float T, float X, float Z)>();

        /// <param name="window">Seconds the speed is measured over.</param>
        public HeadSpeedMeter(float window)
        {
            _window = window;
        }

        /// <summary>Adds the head position (x, z); returns the speed over the last window, or null while it is not covered yet.</summary>
        public float? Add(float time, float x, float z)
        {
            if (_samples.Count > 0)
            {
                var last = _samples[_samples.Count - 1];
                if (time < last.T || Distance(last.X, last.Z, x, z) > MaxStep)
                {
                    _samples.Clear();
                }
            }
            _samples.Add((time, x, z));
            var anchor = -1;
            for (var i = _samples.Count - 1; i >= 0; i--)
            {
                if (_samples[i].T <= time - _window)
                {
                    anchor = i;
                    break;
                }
            }
            if (anchor < 0)
            {
                return null;
            }
            _samples.RemoveRange(0, anchor);
            var a = _samples[0];
            return Distance(a.X, a.Z, x, z) / (time - a.T);
        }

        public void Clear() => _samples.Clear();

        private static float Distance(float x0, float z0, float x1, float z1)
        {
            var dx = x1 - x0;
            var dz = z1 - z0;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }

    public enum CatchResult
    {
        /// <summary>Not a weapon DMCS knocked away, or it already landed, expired or was caught.</summary>
        NotTracked,

        /// <summary>An NPC grabbed it first; the chance is gone.</summary>
        NotPlayer,

        /// <summary>Grabbed by the player, but later than the air time allows.</summary>
        TooLate,

        Caught,
    }

    /// <summary>
    /// Step 4e: a weapon DMCS knocked out of an NPC's hand, grabbed by the player's hand before it touched the ground
    /// (and within the air time), is a catch. Keyed by the weapon.
    /// </summary>
    public sealed class CatchTracker<TId>
    {
        private readonly Dictionary<TId, float> _flying = new Dictionary<TId, float>();

        /// <summary>
        /// What the weapon hit counts as ground when it belongs to no ColliderGroup: no creature part and no item, i.e.
        /// level geometry and static props (the game's own CollisionHandler tells them apart the same way).
        /// </summary>
        public static bool IsGround(bool otherHasColliderGroup) => !otherHasColliderGroup;

        public void KnockedAway(TId weapon, float now) => _flying[weapon] = now;

        public bool IsTracked(TId weapon) => _flying.ContainsKey(weapon);

        public void Landed(TId weapon) => _flying.Remove(weapon);

        public CatchResult Grabbed(TId weapon, float now, bool byPlayer, float maxAirTime)
        {
            if (!_flying.TryGetValue(weapon, out var since))
            {
                return CatchResult.NotTracked;
            }
            _flying.Remove(weapon);
            if (!byPlayer)
            {
                return CatchResult.NotPlayer;
            }
            return now - since <= maxAirTime ? CatchResult.Caught : CatchResult.TooLate;
        }

        /// <summary>Weapons in the air longer than the air time: no catch possible any more; forgotten.</summary>
        public List<TId> Expired(float now, float maxAirTime)
        {
            var expired = _flying.Where(p => now - p.Value > maxAirTime).Select(p => p.Key).ToList();
            foreach (var weapon in expired)
            {
                _flying.Remove(weapon);
            }
            return expired;
        }

        /// <summary>Forgets every weapon; returns them so the caller can clean up.</summary>
        public List<TId> Clear()
        {
            var all = _flying.Keys.ToList();
            _flying.Clear();
            return all;
        }
    }
}
