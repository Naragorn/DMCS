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
    /// Disarm Strike: a player hit on the hand (optionally forearm) of a staggered NPC makes it drop its weapon(s).
    /// "Staggered" is judged as it was when the hit started (EventTime.OnStart), so the stagger this very hit
    /// causes (Charge Stagger, the game's own push) does not count: stagger first, then disarm.
    /// Release: RagdollHand.TryRelease, then EventManager.InvokeDisarm like the game's own hand-hit disarm
    /// (BrainModuleEquipment.OnDamage). While disarmed, any grab is released again (no picking up, no drawing),
    /// and with "Stand Still" the NPC gets the game's passive HumanDummy brain (Brain.Load) until the time ends.
    /// Body Hit Drop: a player hit anywhere but the hand on a staggered NPC makes it drop its weapon(s) with a chance
    /// that rises with every earlier such hit in the same stagger; a hit that does not drop shows sparks and a pulse.
    /// </summary>
    public static class DisarmStrike
    {
        private const string Channel = "Disarm Strike";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.DisarmStrike, true, Dmcs.Log);

        private static readonly StaggerWindows<int> DisarmedWindows = new StaggerWindows<int>();
        private static readonly Dictionary<int, Creature> Creatures = new Dictionary<int, Creature>();
        private static readonly Dictionary<int, DisarmHands> EmptyHands = new Dictionary<int, DisarmHands>();
        private static readonly Dictionary<int, string> OriginalBrains = new Dictionary<int, string>();
        private static readonly StaggerHitCounts<int> BodyHits = new StaggerHitCounts<int>();
        private static readonly Dictionary<int, Creature> BodyHitCreatures = new Dictionary<int, Creature>();
        private static readonly System.Random Dice = new System.Random();
        private static bool _hooked;

        /// <summary>Raised for each disarm (Adrenaline can gain from it).</summary>
        public static event Action<Creature> Disarmed;

        [ModOption(
            name = DmcsCategories.DisarmStrike + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Hitting a staggered enemy's hand makes them drop their weapon.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }

        [ModOption(
            name = DisarmStrikeSettings.RequireStaggerName,
            tooltip = "Only a staggered (or knocked-down) enemy can be disarmed. Off: any hand hit disarms.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 1)]
        public static bool RequireStagger { get; set; } = DisarmStrikeSettings.DefaultRequireStagger;

        [ModOption(
            name = DisarmStrikeSettings.IncludeForearmName,
            tooltip = "Hits on the forearm disarm too, not only hits on the hand.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 2)]
        public static bool IncludeForearm { get; set; } = DisarmStrikeSettings.DefaultIncludeForearm;

        [ModOption(
            name = DisarmStrikeSettings.DropBothHandsName,
            tooltip = "The enemy drops everything it holds. Off: only the hit hand lets go.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 3)]
        public static bool DropBothHands { get; set; } = DisarmStrikeSettings.DefaultDropBothHands;

        [ModOption(
            name = DisarmStrikeSettings.ChanceName,
            tooltip = "Chance (%) that a qualifying hand hit disarms.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 4)]
        [ModOptionSlider]
        public static int Chance { get; set; } = DisarmStrikeSettings.DefaultChancePercent;

        [ModOption(
            name = DisarmStrikeSettings.NoRegrabTimeName,
            tooltip = "Seconds a disarmed enemy cannot pick up or draw a weapon (and stands still, if set).",
            valueSourceName = nameof(NoRegrabTimeValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 5)]
        [ModOptionSlider]
        public static float NoRegrabTime { get; set; } = DisarmStrikeSettings.NoRegrabTime.Default;

        [ModOption(
            name = DisarmStrikeSettings.StayDisarmedName,
            tooltip = "A disarmed enemy never re-arms (ignores the time above).",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 6)]
        public static bool StayDisarmed { get; set; } = DisarmStrikeSettings.DefaultStayDisarmed;

        [ModOption(
            name = DisarmStrikeSettings.StandStillName,
            tooltip = "A disarmed enemy just stands there: no fleeing, no fist fighting. Off: it keeps its normal behaviour, only unarmed.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 7)]
        public static bool StandStill { get; set; } = DisarmStrikeSettings.DefaultStandStill;

        [ModOption(
            name = DisarmStrikeSettings.BodyHitDropName,
            tooltip = "Hitting a staggered enemy anywhere but the hand can make them drop their weapon too.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 8)]
        public static bool BodyHitDrop { get; set; } = DisarmStrikeSettings.DefaultBodyHitDrop;

        [ModOption(
            name = DisarmStrikeSettings.BodyHitDropChanceName,
            tooltip = "Chance (%) that the first body hit in a stagger makes the enemy drop their weapon.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 9)]
        [ModOptionSlider]
        public static int BodyHitDropChance { get; set; } = DisarmStrikeSettings.DefaultBodyHitDropChance;

        [ModOption(
            name = DisarmStrikeSettings.DropChancePerExtraHitName,
            tooltip = "Each further body hit in the same stagger adds this much (%) to the drop chance.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 10)]
        [ModOptionSlider]
        public static int DropChancePerExtraHit { get; set; } = DisarmStrikeSettings.DefaultDropChancePerExtraHit;

        [ModOption(
            name = DisarmStrikeSettings.LoosenedSparkName,
            tooltip = "Sparks at the enemy's weapon when a body hit loosens their grip without making them drop it.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 11)]
        public static bool LoosenedSpark { get; set; } = DisarmStrikeSettings.DefaultLoosenedSpark;

        [ModOption(
            name = DisarmStrikeSettings.LoosenedHapticsName,
            tooltip = "A pulse in the hitting hand when a body hit loosens the enemy's grip without making them drop it.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 12)]
        public static bool LoosenedHaptics { get; set; } = DisarmStrikeSettings.DefaultLoosenedHaptics;

        [ModOption(
            name = DisarmStrikeSettings.ShieldsName,
            tooltip = "Shields can be knocked away too. Off: a hand holding a shield keeps it, as in the base game.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 13)]
        public static bool ShieldsCanBeDisarmed { get; set; } = DisarmStrikeSettings.DefaultShieldsCanBeDisarmed;

        [ModOption(
            name = DisarmStrikeSettings.FlingForceName,
            tooltip = "How hard a weapon knocked out of the hand flies away in the hit's direction (m/s). 0: it just drops.",
            valueSourceName = nameof(FlingForceValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 14)]
        [ModOptionSlider]
        public static float FlingForce { get; set; } = DisarmStrikeSettings.FlingForce.Default;

        [ModOption(
            name = DisarmStrikeSettings.FemaleChanceName,
            tooltip = "First body hit drop chance (%) for female enemies. The highest chance that fits the enemy applies.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 15)]
        [ModOptionSlider]
        public static int FemaleChance { get; set; } = DisarmStrikeSettings.DefaultFemaleChance;

        [ModOption(
            name = DisarmStrikeSettings.SmallerChanceName,
            tooltip = "First body hit drop chance (%) for enemies shorter than you (by at least the margin below). The highest chance that fits the enemy applies.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 16)]
        [ModOptionSlider]
        public static int SmallerChance { get; set; } = DisarmStrikeSettings.DefaultSmallerChance;

        [ModOption(
            name = DisarmStrikeSettings.SmallerMarginName,
            tooltip = "How much shorter than you (cm) an enemy must be for the smaller chance. 0: any shorter.",
            valueSourceName = nameof(SmallerMarginValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 17)]
        [ModOptionSlider]
        public static float SmallerMargin { get; set; } = DisarmStrikeSettings.SmallerMargin.Default;


        public static ModOptionInt[] ChanceOptionValues() =>
            DisarmStrikeSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture) + " %", v)).ToArray();

        public static ModOptionFloat[] NoRegrabTimeValues() =>
            DisarmStrikeSettings.NoRegrabTime.Values()
                .Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " s", v)).ToArray();

        public static ModOptionFloat[] SmallerMarginValues() =>
            DisarmStrikeSettings.SmallerMargin.Values()
                .Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " cm", v)).ToArray();

        public static ModOptionFloat[] FlingForceValues() =>
            DisarmStrikeSettings.FlingForce.Values()
                .Select(v => new ModOptionFloat(v.ToString("0.0", CultureInfo.InvariantCulture) + " m/s", v)).ToArray();

        /// <summary>True while the NPC is disarmed by DMCS.</summary>
        public static bool IsDisarmed(Creature creature)
        {
            return creature != null && DisarmedWindows.IsStaggered(creature.GetInstanceID(), Time.time);
        }

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (!_hooked)
            {
                EventManager.onCreatureHit += OnCreatureHit;
                EventManager.onCreatureDespawn += OnCreatureDespawn;
                _hooked = true;
            }
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureHit -= OnCreatureHit;
                EventManager.onCreatureDespawn -= OnCreatureDespawn;
                _hooked = false;
            }
            RestoreAll();
        }

        /// <summary>Feature off or level unloading: every disarmed NPC gets its own brain back.</summary>
        public static void RestoreAll()
        {
            foreach (var id in DisarmedWindows.TakeAll())
            {
                Restore(id);
            }
            BodyHits.Clear();
            BodyHitCreatures.Clear();
        }

        /// <summary>Every frame: keep the disarmed hands empty (a hand that kept its weapon keeps it), give back brains whose time is up.</summary>
        public static void Update()
        {
            foreach (var id in DisarmedWindows.TakeExpired(Time.time))
            {
                Restore(id);
            }
            if (BodyHits.Count > 0)
            {
                BodyHits.EndStaggers(StillStaggered);
                foreach (var id in BodyHitCreatures.Keys.Where(id => BodyHits.Get(id) == 0).ToList())
                {
                    BodyHitCreatures.Remove(id);
                }
            }
            foreach (var pair in Creatures)
            {
                var creature = pair.Value;
                if (creature == null || creature.isKilled)
                {
                    continue;
                }
                if (!EmptyHands.TryGetValue(pair.Key, out var empty))
                {
                    continue;
                }
                if (empty.Left)
                {
                    ReleaseIfHolding(creature, creature.handLeft, "picked up or drew");
                }
                if (empty.Right)
                {
                    ReleaseIfHolding(creature, creature.handRight, "picked up or drew");
                }
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                RestoreAll();
            }
        }

        private static void OnCreatureHit(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            try
            {
                if (target == null || hit == null)
                {
                    return;
                }
                if (eventTime == EventTime.OnStart)
                {
                    HitInfo.RecordStart(target, hit);
                    return;
                }
                Handle(target, hit, HitInfo.WasStaggeredBefore(target, hit));
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Disarm Strike error: " + ex);
            }
        }

        private static void Handle(Creature target, CollisionInstance hit, bool staggered)
        {
            var part = HitInfo.PartOf(hit);
            var source = HitInfo.SourceOf(hit);
            if (!DisarmRule.IsDisarmPart(part, IncludeForearm))
            {
                HandleBodyHit(target, hit, source, part, staggered);
                return;
            }
            var reach = DisarmHands.ToRelease(part, DropBothHands);
            var hands = new DisarmHands(
                reach.Left && !DisarmShield.Keeps(IsShield(target.handLeft), ShieldsCanBeDisarmed),
                reach.Right && !DisarmShield.Keeps(IsShield(target.handRight), ShieldsCanBeDisarmed));
            var holds = (hands.Left && Holds(target.handLeft)) || (hands.Right && Holds(target.handRight));
            var decision = DisarmRule.Decide(new DisarmHit(
                Toggle.Enabled,
                true,
                source,
                target.isPlayer,
                target.isKilled,
                part,
                IncludeForearm,
                RequireStagger,
                staggered,
                holds,
                (float)Dice.NextDouble(),
                Chance / 100f));
            if (DisarmRule.IsQuiet(decision))
            {
                return;
            }
            if (decision != DisarmDecision.Disarm)
            {
                if (part != HitPart.Other)
                {
                    Dmcs.Log.Debug(Channel, "no disarm on " + target.name + ": " + decision + " (" + part + ", staggered " + staggered + ")");
                }
                return;
            }

            var dropped = new List<string>();
            if (hands.Left)
            {
                Release(target, target.handLeft, hit, dropped, true);
            }
            if (hands.Right)
            {
                Release(target, target.handRight, hit, dropped, true);
            }
            MarkDisarmed(target, hands, part.ToString(), dropped);
        }

        /// <summary>After any disarm: no regrab for the set time, the hands kept empty, standing still if set.</summary>
        private static void MarkDisarmed(Creature target, DisarmHands hands, string how, List<string> dropped)
        {
            var id = target.GetInstanceID();
            var seconds = DisarmStrikeSettings.NoRegrabSeconds(StayDisarmed, NoRegrabTime);
            DisarmedWindows.Mark(id, Time.time, seconds);
            Creatures[id] = target;
            EmptyHands[id] = EmptyHands.TryGetValue(id, out var earlier) ? earlier.With(hands) : hands;
            var stands = StandStill && MakePassive(target, id);
            Dmcs.Log.Debug(Channel, "disarmed " + target.name + " (" + how + "): dropped " + string.Join(", ", dropped)
                + ", no regrab " + (StayDisarmed ? "permanent" : seconds.ToString("0", CultureInfo.InvariantCulture) + " s")
                + (stands ? ", stands still" : string.Empty));
            Disarmed?.Invoke(target);
        }

        /// <summary>Body Hit Drop: a body hit on a staggered NPC drops its weapons with a chance that rises per earlier hit.</summary>
        private static void HandleBodyHit(Creature target, CollisionInstance hit, HitSource source, HitPart part, bool staggered)
        {
            var id = target.GetInstanceID();
            var hands = new DisarmHands(
                !DisarmShield.Keeps(IsShield(target.handLeft), ShieldsCanBeDisarmed),
                !DisarmShield.Keeps(IsShield(target.handRight), ShieldsCanBeDisarmed));
            var droppable = (hands.Left && Holds(target.handLeft)) || (hands.Right && Holds(target.handRight));
            var earlier = BodyHits.Get(id);
            var result = BodyHitDropRule.Decide(new BodyHit(
                Toggle.Enabled,
                BodyHitDrop,
                source,
                target.isPlayer,
                target.isKilled,
                part,
                IncludeForearm,
                staggered,
                droppable,
                earlier,
                (float)Dice.NextDouble(),
                BodyHitDropRule.NpcChance(
                    BodyHitDropChance / 100f,
                    target.data != null && target.data.gender == CreatureData.Gender.Female,
                    FemaleChance / 100f,
                    IsSmaller(target),
                    SmallerChance / 100f),
                DropChancePerExtraHit / 100f));
            if (BodyHitDropRule.IsQuiet(result.Decision))
            {
                return;
            }
            var where = hit.damageStruct.hitRagdollPart != null ? hit.damageStruct.hitRagdollPart.type.ToString() : part.ToString();
            if (result.Decision != BodyHitDecision.Drop)
            {
                var signal = string.Empty;
                if (BodyHitDropRule.CountsAsEarlierHit(result.Decision))
                {
                    BodyHits.Add(id);
                    BodyHitCreatures[id] = target;
                    signal = Signal(target, hit);
                }
                Dmcs.Log.Debug(Channel, "body hit on " + target.name + " (" + where + "): " + result.Decision
                    + (result.Decision == BodyHitDecision.ChanceFailed ? " at " + Percent(result.Chance) + ", hit " + (earlier + 1) + " in this stagger" : string.Empty)
                    + signal);
                return;
            }
            BodyHits.Forget(id);
            BodyHitCreatures.Remove(id);
            var dropped = new List<string>();
            if (hands.Left)
            {
                Release(target, target.handLeft, hit, dropped, true);
            }
            if (hands.Right)
            {
                Release(target, target.handRight, hit, dropped, true);
            }
            MarkDisarmed(target, hands, "body hit " + where + ", " + Percent(result.Chance) + ", hit " + (earlier + 1) + " in this stagger", dropped);
        }

        /// <summary>The grip loosened but held: sparks at the held weapons and a pulse in the hitting hand, as set.</summary>
        private static string Signal(Creature target, CollisionInstance hit)
        {
            var sparks = LoosenedSpark ? Spark(target.handLeft) + Spark(target.handRight) : 0;
            var hand = LoosenedHaptics ? PlayerHandOf(hit) : null;
            if (hand != null)
            {
                hand.HapticTick(1f);
            }
            return (sparks > 0 ? ", sparks " + sparks : string.Empty) + (hand != null ? ", haptic pulse" : string.Empty);
        }

        private static int Spark(RagdollHand hand)
        {
            if (!Holds(hand))
            {
                return 0;
            }
            var data = Catalog.GetData<EffectData>(DisarmStrikeSettings.SparkEffectId, false);
            var instance = data?.Spawn(hand.grabbedHandle.transform.position, Quaternion.identity);
            if (instance == null)
            {
                return 0;
            }
            instance.Play();
            return 1;
        }

        /// <summary>The player's hand behind a hit: the hand holding (or last holding) the item, or the punching hand.</summary>
        private static RagdollHand PlayerHandOf(CollisionInstance hit)
        {
            var player = Player.currentCreature;
            var handler = hit.damageStruct.damager != null ? hit.damageStruct.damager.collisionHandler : null;
            if (handler == null && hit.sourceColliderGroup != null)
            {
                handler = hit.sourceColliderGroup.collisionHandler;
            }
            if (player == null || handler == null)
            {
                return null;
            }
            if (handler.item != null)
            {
                var holding = handler.item.handlers.FirstOrDefault(h => h != null && h.creature == player);
                return holding ?? (handler.item.lastHandler != null && handler.item.lastHandler.creature == player ? handler.item.lastHandler : null);
            }
            return handler.ragdollPart is RagdollHand punching && punching.creature == player ? punching : null;
        }

        /// <summary>Creature.GetHeight = morphology.height * lossyScale.y, for the NPC and the player's body alike.</summary>
        private static bool IsSmaller(Creature target)
        {
            var player = Player.currentCreature;
            return player != null && SizeFilter.IsSmaller(target.GetHeight(), player.GetHeight(), SmallerMargin / 100f);
        }

        private static bool StillStaggered(int id)
        {
            return BodyHitCreatures.TryGetValue(id, out var creature) && creature != null && !creature.isKilled
                && HitInfo.IsStaggered(creature);
        }

        private static string Percent(float chance)
        {
            return (chance * 100f).ToString("0", CultureInfo.InvariantCulture) + " %";
        }

        private static bool IsShield(RagdollHand hand)
        {
            return Holds(hand) && IsShieldItem(hand.grabbedHandle.item);
        }

        /// <summary>As the game's own hand-hit disarm tells a shield (BrainModuleEquipment.OnDamage).</summary>
        private static bool IsShieldItem(Item item)
        {
            return item != null && item.data != null && item.data.moduleAI != null
                && item.data.moduleAI.primaryClass == ItemModuleAI.WeaponClass.Shield;
        }

        private static bool Holds(RagdollHand hand)
        {
            return hand != null && hand.grabbedHandle != null && hand.grabbedHandle.item != null;
        }

        private static void Release(Creature target, RagdollHand hand, CollisionInstance hit, List<string> dropped, bool fling)
        {
            if (!Holds(hand))
            {
                return;
            }
            var handle = hand.grabbedHandle;
            var item = handle.item;
            hand.TryRelease();
            EventManager.InvokeDisarm(target, hand, handle, hit);
            var name = item.data != null ? item.data.id : item.name;
            if (fling && hit != null && FlingForce > 0f && item.handlers.Count == 0 && item.physicBody != null)
            {
                var direction = hit.impactVelocity.sqrMagnitude > 0.0001f
                    ? hit.impactVelocity.normalized
                    : (item.transform.position - target.transform.position).normalized;
                item.physicBody.AddForce(direction * FlingForce, ForceMode.VelocityChange);
                name += " (flung at " + FlingForce.ToString("0.0", CultureInfo.InvariantCulture) + " m/s)";
            }
            dropped.Add(name);
        }

        private static void ReleaseIfHolding(Creature creature, RagdollHand hand, string what)
        {
            if (!Holds(hand))
            {
                return;
            }
            var item = hand.grabbedHandle.item;
            hand.TryRelease();
            Dmcs.Log.Debug(Channel, creature.name + " " + what + " '" + (item.data != null ? item.data.id : item.name) + "' while disarmed: released");
        }

        /// <summary>The game's passive brain (Brain.Load swaps brains at runtime); the original id is kept to give it back.</summary>
        private static bool MakePassive(Creature target, int id)
        {
            var brain = target.brain;
            var current = brain != null && brain.instance != null ? brain.instance.id : null;
            if (brain == null || current == null)
            {
                return false;
            }
            if (current == DisarmStrikeSettings.PassiveBrainId)
            {
                return true;
            }
            if (!OriginalBrains.ContainsKey(id))
            {
                OriginalBrains[id] = current;
            }
            brain.Load(DisarmStrikeSettings.PassiveBrainId);
            return brain.instance != null && brain.instance.id == DisarmStrikeSettings.PassiveBrainId;
        }

        private static void Restore(int id)
        {
            Creatures.TryGetValue(id, out var creature);
            Creatures.Remove(id);
            EmptyHands.Remove(id);
            if (!OriginalBrains.TryGetValue(id, out var original))
            {
                if (creature != null && !creature.isKilled)
                {
                    Dmcs.Log.Debug(Channel, creature.name + " may re-arm");
                }
                return;
            }
            OriginalBrains.Remove(id);
            if (creature == null || creature.isKilled || creature.brain == null)
            {
                return;
            }
            creature.brain.Load(original);
            Dmcs.Log.Debug(Channel, creature.name + " may re-arm: brain '" + original + "' back");
        }

        private static void OnCreatureDespawn(Creature creature, EventTime eventTime)
        {
            if (creature == null || eventTime != EventTime.OnStart)
            {
                return;
            }
            // Pooled creatures load their own brain again on reuse (Creature.Load -> brain.Load(data.brainId)).
            var id = creature.GetInstanceID();
            DisarmedWindows.Remove(id);
            Creatures.Remove(id);
            EmptyHands.Remove(id);
            OriginalBrains.Remove(id);
            BodyHits.Forget(id);
            BodyHitCreatures.Remove(id);
        }
    }
}
