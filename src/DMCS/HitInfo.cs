using System.Collections.Generic;
using System.Linq;
using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>Facts about a hit and its target, read from the game, shared by the features.</summary>
    internal static class HitInfo
    {
        private static readonly Dictionary<CollisionInstance, bool> StaggeredAtStart = new Dictionary<CollisionInstance, bool>();

        /// <summary>
        /// Call on the hit's EventTime.OnStart: remembers whether the target was staggered before this hit.
        /// The game's own hit reaction pushes in between (Creature.OnDamageEvent OnEnd runs before
        /// EventManager.onCreatureHit OnEnd), so at OnEnd the target may be staggered by this very hit.
        /// </summary>
        public static void RecordStart(Creature target, CollisionInstance hit)
        {
            if (!StaggeredAtStart.ContainsKey(hit))
            {
                StaggeredAtStart[hit] = IsStaggered(target);
            }
        }

        /// <summary>Staggered before this hit (falls back to now if OnStart was not seen).</summary>
        public static bool WasStaggeredBefore(Creature target, CollisionInstance hit)
        {
            return StaggeredAtStart.TryGetValue(hit, out var before) ? before : IsStaggered(target);
        }

        /// <summary>Once per frame: Creature.Damage raises OnStart and OnEnd in the same call, so nothing older is needed.</summary>
        public static void EndFrame()
        {
            StaggeredAtStart.Clear();
        }

        /// <summary>
        /// Who dealt the hit. A held item: Item.handlers holds the hands holding it. A thrown item keeps
        /// Item.lastHandler (no hand on it now). Spell projectiles are items with ItemMagicProjectile whose
        /// lastHandler is the casting hand (SpellCastProjectile); other spells set CollisionInstance.casterHand.
        /// A body part: the damager's collision handler is a ragdoll part of the player.
        /// </summary>
        public static HitSource SourceOf(CollisionInstance hit)
        {
            var player = Player.currentCreature;
            if (player == null)
            {
                return HitSource.NotPlayer;
            }
            var handler = hit.damageStruct.damager != null ? hit.damageStruct.damager.collisionHandler : null;
            if (handler == null && hit.sourceColliderGroup != null)
            {
                handler = hit.sourceColliderGroup.collisionHandler;
            }
            var item = handler != null ? handler.item : null;
            if (item != null)
            {
                if (item.handlers.Any(h => h != null && h.creature == player))
                {
                    return HitSource.HeldWeapon;
                }
                if (item.lastHandler == null || item.lastHandler.creature != player)
                {
                    return HitSource.NotPlayer;
                }
                return item.GetComponent<ItemMagicProjectile>() != null ? HitSource.PlayerSpell : HitSource.PlayerThrown;
            }
            var part = handler != null ? handler.ragdollPart : null;
            if (part != null && part.ragdoll != null && part.ragdoll.creature == player)
            {
                return HitSource.PlayerBody;
            }
            if (hit.casterHand != null && hit.casterHand.mana != null && hit.casterHand.mana.creature == player)
            {
                return HitSource.PlayerSpell;
            }
            return HitSource.NotPlayer;
        }

        /// <summary>Inside a Charge Stagger window, staggered by the game itself (Brain.currentStagger), or lying down.</summary>
        public static bool IsStaggered(Creature creature)
        {
            return ChargeStagger.IsStaggered(creature)
                || (creature.brain != null && creature.brain.currentStagger != Brain.Stagger.None)
                || creature.state == Creature.State.Destabilized;
        }

        /// <summary>A kick: the hit came from a foot or leg part of a ragdoll (the player's, as <see cref="SourceOf"/> tells).</summary>
        public static bool IsByFootOrLeg(CollisionInstance hit)
        {
            var handler = hit.damageStruct.damager != null ? hit.damageStruct.damager.collisionHandler : null;
            if (handler == null && hit.sourceColliderGroup != null)
            {
                handler = hit.sourceColliderGroup.collisionHandler;
            }
            var part = handler != null ? handler.ragdollPart : null;
            if (part == null)
            {
                return false;
            }
            switch (part.type)
            {
                case RagdollPart.Type.LeftFoot:
                case RagdollPart.Type.RightFoot:
                case RagdollPart.Type.LeftLeg:
                case RagdollPart.Type.RightLeg:
                    return true;
                default:
                    return false;
            }
        }

        public static HitPart PartOf(CollisionInstance hit)
        {
            var part = hit.damageStruct.hitRagdollPart;
            if (part == null)
            {
                return HitPart.Other;
            }
            switch (part.type)
            {
                case RagdollPart.Type.LeftHand: return HitPart.LeftHand;
                case RagdollPart.Type.RightHand: return HitPart.RightHand;
                case RagdollPart.Type.LeftArm: return HitPart.LeftArm;
                case RagdollPart.Type.RightArm: return HitPart.RightArm;
                default: return HitPart.Other;
            }
        }
    }
}
