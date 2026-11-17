using System;
using System.Collections.Generic;
using System.Globalization;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Step 4e: two more Adrenaline sources.
    /// Dodge: an NPC melee attack on the player (EventManager.onCreatureAttacking, raised by BrainModuleMelee) that
    /// reached AttackStage.Attack and ends with AttackStage.End (Cancel = interrupted) without hitting the player and
    /// without a block or parry, while the player's head moved fast (horizontal head speed: a VR dodge is mostly a real
    /// step or lean, which Locomotion's stick speed does not show). The game has no dodge event; an approximation.
    /// Catch: a weapon a DMCS disarm knocked out of an NPC's hand, grabbed by the player's hand (EventManager.OnItemGrab)
    /// before it touched the ground. "Ground" is read with an own OnCollisionEnter component on the weapon's rigidbody:
    /// the game's CollisionHandler drops slow contacts and contacts without known physics materials.
    /// </summary>
    internal static class AdrenalineDodgeCatch
    {
        private const string Channel = "Adrenaline";

        /// <summary>Seconds of head speed kept (longer than any melee attack from Attack to End).</summary>
        private const float SpeedRetention = 5f;

        private static readonly DodgeTracker<int> Dodges = new DodgeTracker<int>();
        private static readonly SpeedHistory HeadSpeeds = new SpeedHistory(SpeedRetention);

        /// <summary>Seconds the head speed is measured over (a few physics steps; see HeadSpeedMeter).</summary>
        private const float SpeedWindow = 0.1f;

        private static readonly HeadSpeedMeter HeadMeter = new HeadSpeedMeter(SpeedWindow);
        private static readonly CatchTracker<int> Catches = new CatchTracker<int>();
        private static readonly Dictionary<int, Item> Flying = new Dictionary<int, Item>();
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            EventManager.onCreatureAttacking += OnCreatureAttacking;
            EventManager.OnItemGrab += OnItemGrab;
            DisarmStrike.KnockedAway += OnKnockedAway;
            _hooked = true;
        }

        public static void Unhook()
        {
            if (!_hooked)
            {
                return;
            }
            EventManager.onCreatureAttacking -= OnCreatureAttacking;
            EventManager.OnItemGrab -= OnItemGrab;
            DisarmStrike.KnockedAway -= OnKnockedAway;
            _hooked = false;
            ResetAll();
        }

        public static void ResetAll()
        {
            Dodges.Clear();
            HeadSpeeds.Clear();
            HeadMeter.Clear();
            foreach (var id in Catches.Clear())
            {
                StopWatching(id);
            }
        }

        /// <summary>Every frame while Adrenaline is on: sample the head speed, forget weapons past the air time.</summary>
        public static void Update()
        {
            var now = Time.unscaledTime;
            if (Player.local != null && Player.local.head != null)
            {
                var head = Player.local.head.transform.position;
                var speed = HeadMeter.Add(now, head.x, head.z);
                if (speed.HasValue)
                {
                    HeadSpeeds.Add(now, speed.Value);
                }
            }
            foreach (var id in Catches.Expired(now, Adrenaline.CatchMaxAirTime))
            {
                StopWatching(id);
            }
        }

        /// <summary>An NPC hit on the player (any phase): the attacker's open attack is no dodge.</summary>
        public static void PlayerHit(CollisionInstance hit)
        {
            var attacker = HitInfo.AttackerOf(hit);
            if (attacker != null)
            {
                Dodges.PlayerHitBy(attacker.GetInstanceID());
            }
            else
            {
                Dodges.PlayerHitByUnknown();
            }
        }

        /// <summary>The player blocked or parried this NPC's attack.</summary>
        public static void Parried(Creature attacker) => Dodges.Parried(attacker.GetInstanceID());

        private static void OnCreatureAttacking(Creature attacker, Creature target, Transform targetTransform,
            BrainModuleAttack.AttackType type, BrainModuleAttack.AttackStage stage)
        {
            try
            {
                if (!Adrenaline.Toggle.Enabled || attacker == null || attacker.isPlayer || type != BrainModuleAttack.AttackType.Melee)
                {
                    return;
                }
                var id = attacker.GetInstanceID();
                var now = Time.unscaledTime;
                switch (stage)
                {
                    case BrainModuleAttack.AttackStage.Attack:
                        if (target != null && target.isPlayer)
                        {
                            Dodges.AttackStarted(id, now);
                        }
                        break;
                    case BrainModuleAttack.AttackStage.Cancel:
                        Dodges.AttackCancelled(id);
                        break;
                    case BrainModuleAttack.AttackStage.End:
                        Judge(attacker, id, now);
                        break;
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline dodge error: " + ex);
            }
        }

        private static void Judge(Creature attacker, int id, float now)
        {
            if (!Dodges.TryStartedAt(id, out var startedAt))
            {
                return;
            }
            var peak = HeadSpeeds.Peak(now, now - startedAt);
            var result = Dodges.AttackEnded(id, peak, Adrenaline.DodgeMinSpeed);
            var speed = peak.ToString("0.00", CultureInfo.InvariantCulture) + " m/s";
            if (result == DodgeResult.Dodged)
            {
                Dmcs.Log.Debug(Channel, "dodged " + attacker.name + "'s attack (head " + speed + ")");
                Adrenaline.Add(AdrenalineSource.Dodge, attacker.name);
            }
            else
            {
                Dmcs.Log.Debug(Channel, "no dodge of " + attacker.name + "'s attack: " + result + " (head " + speed + ")");
            }
        }

        private static void OnKnockedAway(Item item)
        {
            try
            {
                if (!Adrenaline.Toggle.Enabled || item == null || item.physicBody == null || item.physicBody.gameObject == null)
                {
                    return;
                }
                var id = item.GetInstanceID();
                Catches.KnockedAway(id, Time.unscaledTime);
                Flying[id] = item;
                var watch = item.physicBody.gameObject.GetComponent<CatchWatch>() ?? item.physicBody.gameObject.AddComponent<CatchWatch>();
                watch.ItemId = id;
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline catch error: " + ex);
            }
        }

        private static void OnItemGrab(Handle handle, RagdollHand hand)
        {
            try
            {
                var item = handle != null ? handle.item : null;
                if (!Adrenaline.Toggle.Enabled || item == null || hand == null)
                {
                    return;
                }
                var id = item.GetInstanceID();
                var name = item.data != null ? item.data.id : item.name;
                var result = Catches.Grabbed(id, Time.unscaledTime, hand.creature != null && hand.creature.isPlayer, Adrenaline.CatchMaxAirTime);
                if (result == CatchResult.NotTracked)
                {
                    return;
                }
                StopWatching(id);
                if (result == CatchResult.Caught)
                {
                    Dmcs.Log.Debug(Channel, "caught " + name + " in the air");
                    Adrenaline.Add(AdrenalineSource.Catch, name);
                }
                else
                {
                    Dmcs.Log.Debug(Channel, "no catch bonus for " + name + ": " + result);
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Adrenaline catch error: " + ex);
            }
        }

        /// <summary>The weapon touched level geometry or a static prop: no catch any more.</summary>
        internal static void Landed(int id)
        {
            if (!Catches.IsTracked(id))
            {
                return;
            }
            Catches.Landed(id);
            var name = Flying.TryGetValue(id, out var item) && item != null && item.data != null ? item.data.id : "weapon";
            StopWatching(id);
            Dmcs.Log.Debug(Channel, name + " touched the ground: no catch bonus");
        }

        private static void StopWatching(int id)
        {
            if (Flying.TryGetValue(id, out var item) && item != null && item.physicBody != null && item.physicBody.gameObject != null)
            {
                var watch = item.physicBody.gameObject.GetComponent<CatchWatch>();
                if (watch != null)
                {
                    UnityEngine.Object.Destroy(watch);
                }
            }
            Flying.Remove(id);
        }
    }

    /// <summary>On a knocked-away weapon's rigidbody: reports the first contact with something that is no creature and no item.</summary>
    internal sealed class CatchWatch : MonoBehaviour
    {
        public int ItemId;

        private void OnCollisionEnter(Collision collision)
        {
            var other = collision.collider;
            if (other == null || !CatchTracker<int>.IsGround(other.GetComponentInParent<ColliderGroup>() != null))
            {
                return;
            }
            AdrenalineDodgeCatch.Landed(ItemId);
        }
    }
}
