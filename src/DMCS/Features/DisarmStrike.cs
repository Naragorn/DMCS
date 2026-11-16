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
    /// </summary>
    public static class DisarmStrike
    {
        private const string Channel = "Disarm Strike";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.DisarmStrike, true, Dmcs.Log);

        private static readonly StaggerWindows<int> DisarmedWindows = new StaggerWindows<int>();
        private static readonly Dictionary<int, Creature> Creatures = new Dictionary<int, Creature>();
        private static readonly Dictionary<int, string> OriginalBrains = new Dictionary<int, string>();
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

        public static ModOptionInt[] ChanceOptionValues() =>
            DisarmStrikeSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture) + " %", v)).ToArray();

        public static ModOptionFloat[] NoRegrabTimeValues() =>
            DisarmStrikeSettings.NoRegrabTime.Values()
                .Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " s", v)).ToArray();

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
        }

        /// <summary>Every frame: keep disarmed hands empty, give back brains whose time is up.</summary>
        public static void Update()
        {
            foreach (var id in DisarmedWindows.TakeExpired(Time.time))
            {
                Restore(id);
            }
            foreach (var pair in Creatures)
            {
                var creature = pair.Value;
                if (creature == null || creature.isKilled)
                {
                    continue;
                }
                ReleaseIfHolding(creature, creature.handLeft, "picked up or drew");
                ReleaseIfHolding(creature, creature.handRight, "picked up or drew");
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
            var hands = DisarmHands.ToRelease(part, DropBothHands);
            var holds = (hands.Left && Holds(target.handLeft)) || (hands.Right && Holds(target.handRight));
            var decision = DisarmRule.Decide(new DisarmHit(
                Toggle.Enabled,
                true,
                HitInfo.SourceOf(hit),
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
                Release(target, target.handLeft, hit, dropped);
            }
            if (hands.Right)
            {
                Release(target, target.handRight, hit, dropped);
            }
            var id = target.GetInstanceID();
            var seconds = DisarmStrikeSettings.NoRegrabSeconds(StayDisarmed, NoRegrabTime);
            DisarmedWindows.Mark(id, Time.time, seconds);
            Creatures[id] = target;
            var stands = StandStill && MakePassive(target, id);
            Dmcs.Log.Debug(Channel, "disarmed " + target.name + " (" + part + "): dropped " + string.Join(", ", dropped)
                + ", no regrab " + (StayDisarmed ? "permanent" : seconds.ToString("0", CultureInfo.InvariantCulture) + " s")
                + (stands ? ", stands still" : string.Empty));
            Disarmed?.Invoke(target);
        }

        private static bool Holds(RagdollHand hand)
        {
            return hand != null && hand.grabbedHandle != null && hand.grabbedHandle.item != null;
        }

        private static void Release(Creature target, RagdollHand hand, CollisionInstance hit, List<string> dropped)
        {
            if (!Holds(hand))
            {
                return;
            }
            var handle = hand.grabbedHandle;
            dropped.Add(handle.item.data != null ? handle.item.data.id : handle.item.name);
            hand.TryRelease();
            EventManager.InvokeDisarm(target, hand, handle, hit);
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
            if (!OriginalBrains.TryGetValue(id, out var original))
            {
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
            OriginalBrains.Remove(id);
        }
    }
}
