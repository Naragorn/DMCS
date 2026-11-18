using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Twist Disarm (spec 2b, part of Disarm Strike): the player's held weapon hits an enemy's held weapon while the
    /// player's wrist rolls the blade around its own long axis faster than *Twist Min Roll Speed*: the enemy drops it
    /// with *Twist Disarm Chance*, no stagger needed. Blade contacts come from the player's weapons' own collision
    /// handlers (CollisionHandler.OnCollisionStartEvent, subscribed while the player holds them, as the game's own
    /// BladeDancer does); the roll from the controller's rig-local rotation (HandSpeeds.PeakRoll).
    /// </summary>
    internal static class TwistDisarm
    {
        private const string Channel = "Disarm Strike";

        private static readonly System.Random Dice = new System.Random();
        private static readonly ContactCooldown<int> Cooldown = new ContactCooldown<int>();
        private static readonly HashSet<Item> Watched = new HashSet<Item>();
        private static bool _hooked;

        public static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            EventManager.OnItemGrab += OnItemGrab;
            EventManager.OnItemRelease += OnItemRelease;
            _hooked = true;
            var player = Player.currentCreature;
            if (player != null)
            {
                Watch(player.handLeft?.grabbedHandle?.item);
                Watch(player.handRight?.grabbedHandle?.item);
            }
        }

        public static void Unhook()
        {
            if (!_hooked)
            {
                return;
            }
            EventManager.OnItemGrab -= OnItemGrab;
            EventManager.OnItemRelease -= OnItemRelease;
            foreach (var item in Watched.ToList())
            {
                Unwatch(item);
            }
            Cooldown.Clear();
            _hooked = false;
        }

        private static void OnItemGrab(Handle handle, RagdollHand hand)
        {
            if (hand != null && hand.creature != null && hand.creature.isPlayer)
            {
                Watch(handle?.item);
            }
        }

        private static void OnItemRelease(Handle handle, RagdollHand hand, bool throwing)
        {
            var item = handle?.item;
            if (item != null && !item.handlers.Any(h => h != null && h.creature != null && h.creature.isPlayer))
            {
                Unwatch(item);
            }
        }

        private static void Watch(Item item)
        {
            if (item == null || !Watched.Add(item))
            {
                return;
            }
            foreach (var handler in item.collisionHandlers)
            {
                handler.OnCollisionStartEvent -= OnCollision;
                handler.OnCollisionStartEvent += OnCollision;
            }
        }

        private static void Unwatch(Item item)
        {
            if (item == null || !Watched.Remove(item))
            {
                return;
            }
            foreach (var handler in item.collisionHandlers)
            {
                handler.OnCollisionStartEvent -= OnCollision;
            }
        }

        private static void OnCollision(CollisionInstance hit)
        {
            try
            {
                Handle(hit);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Twist Disarm error: " + ex);
            }
        }

        private static void Handle(CollisionInstance hit)
        {
            var mine = hit?.sourceColliderGroup?.collisionHandler?.item;
            var theirs = hit?.targetColliderGroup?.collisionHandler?.item;
            if (theirs != null && Watched.Contains(theirs) && (mine == null || !Watched.Contains(mine)))
            {
                // The event's source is the other side: swap so "mine" is the player's weapon.
                (mine, theirs) = (theirs, mine);
            }
            var playerHand = mine?.handlers.FirstOrDefault(h => h != null && h.creature != null && h.creature.isPlayer);
            var enemyHand = theirs?.mainHandler;
            var enemy = enemyHand?.creature;
            var now = Time.unscaledTime;
            var id = enemy != null ? enemy.GetInstanceID() : 0;
            var axis = BladeAxis(mine, playerHand);
            var rate = playerHand != null ? HandSpeeds.PeakRoll(playerHand.side, axis, TwistSettings.RollWindow) : 0f;
            var decision = TwistRule.Decide(new TwistContact(
                DisarmStrike.Toggle.Enabled && DisarmStrike.TwistEnabled,
                playerHand != null,
                enemyHand != null && theirs.data != null && theirs.data.type == ItemData.Type.Weapon,
                enemy == null || enemy.isKilled || enemy.isPlayer,
                DisarmShield.Keeps(DisarmStrike.IsShieldItem(theirs), DisarmStrike.ShieldsCanBeDisarmed),
                ClashOfMight.IsLocked,
                enemy != null && Cooldown.IsCoolingDown(id, now),
                rate,
                DisarmStrike.TwistMinRollSpeed,
                (float)Dice.NextDouble(),
                DisarmStrike.TwistChance / 100f));
            if (TwistRule.IsQuiet(decision))
            {
                return;
            }
            Cooldown.Mark(id, now, TwistSettings.ContactCooldown);
            var facts = "roll " + rate.ToString("0", CultureInfo.InvariantCulture) + "°/s (min "
                + DisarmStrike.TwistMinRollSpeed.ToString("0", CultureInfo.InvariantCulture) + "), hit "
                + hit.impactVelocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture) + " m/s";
            if (decision != TwistDecision.Disarm)
            {
                Dmcs.Log.Debug(Channel, "blade on " + enemy.name + "'s " + theirs.data?.id + ": no twist disarm, " + decision + " (" + facts + ")");
                return;
            }
            var along = hit.impactVelocity.sqrMagnitude > 0.0001f ? hit.impactVelocity : theirs.transform.position - playerHand.transform.position;
            if (DisarmStrike.DisarmAlong(enemy, enemyHand, along, "twist, " + facts, hit))
            {
                Dmcs.Log.Debug(Channel, "twist disarm on " + enemy.name + "'s " + theirs.data?.id + " (" + facts + ")");
            }
        }

        /// <summary>The held blade's long axis (handle → furthest damager), world space; the hand's forward when unknown.</summary>
        private static Vector3 BladeAxis(Item item, RagdollHand hand)
        {
            if (item == null || hand == null)
            {
                return Vector3.forward;
            }
            item.GetFurthestDamagerCollider(out _, out var tip);
            var grip = hand.grabbedHandle != null ? hand.grabbedHandle.transform.position : item.transform.position;
            var axis = tip != null ? tip.bounds.center - grip : hand.transform.forward;
            return axis.sqrMagnitude > 0.0001f ? axis.normalized : hand.transform.forward;
        }
    }
}
