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
        private static readonly Dictionary<int, Reaction> Reactions = new Dictionary<int, Reaction>();

        /// <summary>The turn request handler for a backing-off enemy (BrainModuleMove keeps one request per handler).</summary>
        private static readonly object BackOffTurn = new object();
        private static bool _hooked;

        /// <summary>Raised for each disarm (Adrenaline can gain from it).</summary>
        public static event Action<Creature> Disarmed;

        /// <summary>Raised for each weapon a DMCS disarm path released from an NPC's hand (step 4e: the catch bonus).</summary>
        public static event Action<Item> KnockedAway;

        [ModOption(
            name = DmcsCategories.DisarmStrike + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Hitting a staggered enemy's hand makes them drop their weapon.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.MenuEnabled;
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

        [ModOption(name = DisarmStrikeSettings.ShockChanceName, tooltip = "How often a disarmed enemy freezes in shock, against the other two reactions. 0: never. Afterwards it goes for a weapon.", valueSourceName = nameof(ReactionChanceValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 5)]
        [ModOptionSlider]
        public static int ShockChance { get; set; } = DisarmStrikeSettings.DefaultShockChance;

        [ModOption(name = DisarmStrikeSettings.ShockTimeName, tooltip = "Seconds a shocked enemy stands frozen.", valueSourceName = nameof(ShockTimeValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 6)]
        [ModOptionSlider]
        public static float ShockTime { get; set; } = DisarmStrikeSettings.ShockTime.Default;

        [ModOption(name = DisarmStrikeSettings.BackOffChanceName, tooltip = "How often a disarmed enemy backs away from you, against the other two reactions. 0: never. Afterwards it goes for a weapon.", valueSourceName = nameof(ReactionChanceValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 7)]
        [ModOptionSlider]
        public static int BackOffChance { get; set; } = DisarmStrikeSettings.DefaultBackOffChance;

        [ModOption(name = DisarmStrikeSettings.BackOffTimeName, tooltip = "Seconds a disarmed enemy keeps backing away.", valueSourceName = nameof(BackOffTimeValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 8)]
        [ModOptionSlider]
        public static float BackOffTime { get; set; } = DisarmStrikeSettings.BackOffTime.Default;

        [ModOption(name = DisarmStrikeSettings.CowerChanceName, tooltip = "How often a disarmed enemy cowers in fear, against the other two reactions. 0: never. Hitting it ends the cowering.", valueSourceName = nameof(ReactionChanceValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 9)]
        [ModOptionSlider]
        public static int CowerChance { get; set; } = DisarmStrikeSettings.DefaultCowerChance;

        [ModOption(name = DisarmStrikeSettings.CowerTimeName, tooltip = "Seconds a disarmed enemy cowers.", valueSourceName = nameof(CowerTimeValues), category = DmcsCategories.DisarmStrike, categoryOrder = DmcsCategories.DisarmStrikeOrder, order = 10)]
        [ModOptionSlider]
        public static float CowerTime { get; set; } = DisarmStrikeSettings.CowerTime.Default;

        [ModOption(
            name = DisarmStrikeSettings.StayDisarmedName,
            tooltip = "A disarmed enemy never re-arms: after its reaction it just stands there.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 11)]
        public static bool StayDisarmed { get; set; } = DisarmStrikeSettings.DefaultStayDisarmed;

        [ModOption(
            name = DisarmStrikeSettings.BodyHitDropName,
            tooltip = "Hitting a staggered enemy anywhere but the hand can make them drop their weapon too.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 12)]
        public static bool BodyHitDrop { get; set; } = DisarmStrikeSettings.DefaultBodyHitDrop;

        [ModOption(
            name = DisarmStrikeSettings.BodyHitDropChanceName,
            tooltip = "Chance (%) that the first body hit in a stagger makes the enemy drop their weapon.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 13)]
        [ModOptionSlider]
        public static int BodyHitDropChance { get; set; } = DisarmStrikeSettings.DefaultBodyHitDropChance;

        [ModOption(
            name = DisarmStrikeSettings.DropChancePerExtraHitName,
            tooltip = "Each further body hit in the same stagger adds this much (%) to the drop chance.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 14)]
        [ModOptionSlider]
        public static int DropChancePerExtraHit { get; set; } = DisarmStrikeSettings.DefaultDropChancePerExtraHit;

        [ModOption(
            name = DisarmStrikeSettings.LoosenedSparkName,
            tooltip = "Sparks at the enemy's weapon when a body hit loosens their grip without making them drop it.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 15)]
        public static bool LoosenedSpark { get; set; } = DisarmStrikeSettings.DefaultLoosenedSpark;

        [ModOption(
            name = DisarmStrikeSettings.LoosenedHapticsName,
            tooltip = "A pulse in the hitting hand when a body hit loosens the enemy's grip without making them drop it.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 16)]
        public static bool LoosenedHaptics { get; set; } = DisarmStrikeSettings.DefaultLoosenedHaptics;

        [ModOption(
            name = DisarmStrikeSettings.ShieldsName,
            tooltip = "Shields can be knocked away too. Off: a hand holding a shield keeps it, as in the base game.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 17)]
        public static bool ShieldsCanBeDisarmed { get; set; } = DisarmStrikeSettings.DefaultShieldsCanBeDisarmed;

        [ModOption(
            name = DisarmStrikeSettings.FlingForceName,
            tooltip = "How hard a weapon knocked out of the hand flies away in the hit's direction (m/s). 0: it just drops.",
            valueSourceName = nameof(FlingForceValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 18)]
        [ModOptionSlider]
        public static float FlingForce { get; set; } = DisarmStrikeSettings.FlingForce.Default;

        [ModOption(
            name = DisarmStrikeSettings.FemaleChanceName,
            tooltip = "First body hit drop chance (%) for female enemies. The highest chance that fits the enemy applies.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 19)]
        [ModOptionSlider]
        public static int FemaleChance { get; set; } = DisarmStrikeSettings.DefaultFemaleChance;

        [ModOption(
            name = DisarmStrikeSettings.SmallerChanceName,
            tooltip = "First body hit drop chance (%) for enemies shorter than you (by at least the margin below). The highest chance that fits the enemy applies.",
            valueSourceName = nameof(ChanceOptionValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 20)]
        [ModOptionSlider]
        public static int SmallerChance { get; set; } = DisarmStrikeSettings.DefaultSmallerChance;

        [ModOption(
            name = DisarmStrikeSettings.SmallerMarginName,
            tooltip = "How much shorter than you (cm) an enemy must be for the smaller chance. 0: any shorter.",
            valueSourceName = nameof(SmallerMarginValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 21)]
        [ModOptionSlider]
        public static float SmallerMargin { get; set; } = DisarmStrikeSettings.SmallerMargin.Default;


        public static ModOptionInt[] ChanceOptionValues() =>
            DisarmStrikeSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture) + " %", v)).ToArray();

        /// <summary>Relative chances (no %: they are weighed against each other).</summary>
        public static ModOptionInt[] ReactionChanceValues() =>
            DisarmStrikeSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture), v)).ToArray();

        public static ModOptionFloat[] ShockTimeValues() => Seconds(DisarmStrikeSettings.ShockTime);

        public static ModOptionFloat[] BackOffTimeValues() => Seconds(DisarmStrikeSettings.BackOffTime);

        public static ModOptionFloat[] CowerTimeValues() => Seconds(DisarmStrikeSettings.CowerTime);

        private static ModOptionFloat[] Seconds(SliderSpec spec) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " s", v)).ToArray();

        public static ModOptionFloat[] SmallerMarginValues() =>
            DisarmStrikeSettings.SmallerMargin.Values()
                .Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " cm", v)).ToArray();

        [ModOption(
            name = TwistSettings.EnabledName,
            tooltip = "Hit a staggered enemy's weapon with yours while you twist your wrist (the blade rolls around itself), and it drops it.",
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 22)]
        public static bool TwistEnabled { get; set; } = TwistSettings.DefaultEnabled;

        [ModOption(
            name = TwistSettings.ChanceName,
            tooltip = "Chance (percent) that a fast enough twist on a staggered enemy's weapon knocks it out.",
            valueSourceName = nameof(TwistChanceValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 23)]
        [ModOptionSlider]
        public static float TwistChance { get; set; } = TwistSettings.Chance.Default;

        [ModOption(
            name = TwistSettings.MinRollSpeedName,
            tooltip = "How fast (degrees per second) your blade must roll around itself when it meets the enemy's weapon.",
            valueSourceName = nameof(TwistMinRollSpeedValues),
            category = DmcsCategories.DisarmStrike,
            categoryOrder = DmcsCategories.DisarmStrikeOrder,
            order = 24)]
        [ModOptionSlider]
        public static float TwistMinRollSpeed { get; set; } = TwistSettings.MinRollSpeed.Default;

        public static ModOptionFloat[] TwistChanceValues() =>
            TwistSettings.Chance.Values().Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + " %", v)).ToArray();

        public static ModOptionFloat[] TwistMinRollSpeedValues() =>
            TwistSettings.MinRollSpeed.Values().Select(v => new ModOptionFloat(v.ToString("0", CultureInfo.InvariantCulture) + "°/s", v)).ToArray();

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
                TwistDisarm.Hook();
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
                TwistDisarm.Unhook();
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
            UpdateReactions();
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

        /// <summary>
        /// After any disarm (user 2026-11-22: they "just stood there"): the dice pick a reaction (a short shock, backing
        /// off, or cowering) and its time; the hands stay empty for that time, then the enemy gets its own brain back and
        /// goes for a weapon with the game's own behaviour (BehaviorTree_HumanUnarmed: find, walk to, pick up). With Stays
        /// Permanent it stands still for good after the reaction. The reaction starts once a running stagger is over (most
        /// disarms hit a staggered enemy), at most <see cref="DisarmReactions.MaxStaggerWait"/> later.
        /// </summary>
        private static void MarkDisarmed(Creature target, DisarmHands hands, string how, List<string> dropped)
        {
            var id = target.GetInstanceID();
            Creatures[id] = target;
            EmptyHands[id] = EmptyHands.TryGetValue(id, out var earlier) ? earlier.With(hands) : hands;
            string reacts;
            if (Reactions.TryGetValue(id, out var running))
            {
                // The other hand dropped while it still reacts: the same reaction runs on.
                reacts = "still " + Describe(running.Kind);
            }
            else
            {
                var reaction = DisarmReactions.Pick(Dice.NextDouble(), ShockChance, BackOffChance, CowerChance);
                var reactSeconds = DisarmReactions.Seconds(reaction, ShockTime, BackOffTime, CowerTime);
                if (reaction == DisarmReaction.None)
                {
                    DisarmedWindows.Mark(id, Time.time, DisarmStrikeSettings.NoRegrabSeconds(StayDisarmed, 0f));
                    if (StayDisarmed)
                    {
                        MakePassive(target, id);
                    }
                }
                else
                {
                    Reactions[id] = Reaction.Pending(target, reaction, reactSeconds, Time.time);
                    DisarmedWindows.Mark(id, Time.time, DisarmStrikeSettings.NoRegrabSeconds(StayDisarmed, reactSeconds + DisarmReactions.MaxStaggerWait));
                }
                reacts = Describe(reaction) + (reaction == DisarmReaction.None ? "" : " for " + reactSeconds.ToString("0", CultureInfo.InvariantCulture) + " s");
            }
            Dmcs.Log.Debug(Channel, "disarmed " + target.name + " (" + how + "): dropped " + string.Join(", ", dropped) + ", " + reacts
                + (StayDisarmed ? ", then stands still for good" : ", then goes for a weapon"));
            Disarmed?.Invoke(target);
        }

        private static string Describe(DisarmReaction reaction)
        {
            switch (reaction)
            {
                case DisarmReaction.Shock: return "freezes in shock";
                case DisarmReaction.BackOff: return "backs off";
                case DisarmReaction.Cower: return "cowers";
                default: return "no reaction";
            }
        }

        /// <summary>Starts the reaction with the game's own means; a reaction the brain cannot do falls back to the shock.</summary>
        private static DisarmReaction StartReaction(Creature target, int id, DisarmReaction reaction, float seconds)
        {
            var instance = target.brain != null ? target.brain.instance : null;
            switch (reaction)
            {
                case DisarmReaction.BackOff:
                    var move = instance?.GetModule<BrainModuleMove>(false);
                    if (move == null || instance.tree == null || Player.currentCreature == null)
                    {
                        break;
                    }
                    // Its behaviour tree pauses (as BrainModuleFear.Panic does) while DMCS walks it backwards, facing the player.
                    instance.tree.Reset();
                    instance.updateTree = false;
                    move.TurnTo(Player.currentCreature.transform, BackOffTurn);
                    return reaction;
                case DisarmReaction.Cower:
                    var fear = instance?.GetModule<BrainModuleFear>(false);
                    if (fear == null)
                    {
                        break;
                    }
                    // The game's own panic: a scream, the HumanCower animation, ended early by a hit or a push.
                    fear.Panic(seconds);
                    return reaction;
                case DisarmReaction.None:
                    return reaction;
            }
            return MakePassive(target, id) ? DisarmReaction.Shock : DisarmReaction.None;
        }

        /// <summary>
        /// Every frame: starts a waiting reaction once its stagger is over, walks a backing-off enemy, ends a reaction whose
        /// time is up.
        /// </summary>
        private static void UpdateReactions()
        {
            if (Reactions.Count == 0)
            {
                return;
            }
            var now = Time.time;
            foreach (var pair in Reactions.ToList())
            {
                var r = pair.Value;
                if (r.Creature == null || r.Creature.isKilled)
                {
                    Reactions.Remove(pair.Key);
                    continue;
                }
                if (!r.Started)
                {
                    if (DisarmReactions.ReadyToReact(HitInfo.IsStaggered(r.Creature), now - r.MarkedAt))
                    {
                        var kind = StartReaction(r.Creature, pair.Key, r.Kind, r.Seconds);
                        Reactions[pair.Key] = r.Start(kind, now);
                        // The no-regrab window now ends exactly with the reaction.
                        DisarmedWindows.Remove(pair.Key);
                        DisarmedWindows.Mark(pair.Key, now, DisarmStrikeSettings.NoRegrabSeconds(StayDisarmed, r.Seconds));
                        Dmcs.Log.Debug(Channel, r.Creature.name + " " + Describe(kind) + " now (" + r.Seconds.ToString("0", CultureInfo.InvariantCulture)
                            + " s, waited " + (now - r.MarkedAt).ToString("0.0", CultureInfo.InvariantCulture) + " s for its stagger)");
                    }
                    continue;
                }
                if (now >= r.EndsAt)
                {
                    EndReaction(pair.Key);
                    continue;
                }
                if (r.Kind == DisarmReaction.BackOff)
                {
                    r.Creature.brain?.instance?.GetModule<BrainModuleMove>(false)?.UpdateMoveCycleDirection(Vector3.back, DisarmReactions.BackOffSpeedRatio, 0.5f);
                }
            }
        }

        /// <summary>The reaction is over: the enemy moves on its own again, or with Stays Permanent stands still for good.</summary>
        private static void EndReaction(int id)
        {
            if (!Reactions.TryGetValue(id, out var r))
            {
                return;
            }
            Reactions.Remove(id);
            var creature = r.Creature;
            if (!r.Started || creature == null || creature.isKilled || creature.brain == null || creature.brain.instance == null)
            {
                return;
            }
            var instance = creature.brain.instance;
            if (r.Kind == DisarmReaction.BackOff)
            {
                var move = instance.GetModule<BrainModuleMove>(false);
                move?.StopTurn(BackOffTurn);
                move?.StopMove();
                instance.updateTree = true;
            }
            if (r.Kind == DisarmReaction.Cower && StayDisarmed)
            {
                var fear = instance.GetModule<BrainModuleFear>(false);
                if (fear != null && fear.isCowering)
                {
                    fear.StopPanic();
                }
            }
            if (StayDisarmed)
            {
                MakePassive(creature, id);
            }
            Dmcs.Log.Debug(Channel, creature.name + " is done with its reaction (" + Describe(r.Kind) + ")");
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
            return OneShotEffect.Play(DisarmStrikeSettings.SparkEffectId, hand.grabbedHandle.transform.position) ? 1 : 0;
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
        internal static bool IsShieldItem(Item item)
        {
            return item != null && item.data != null && item.data.moduleAI != null
                && item.data.moduleAI.primaryClass == ItemModuleAI.WeaponClass.Shield;
        }

        private static bool Holds(RagdollHand hand)
        {
            return hand != null && hand.grabbedHandle != null && hand.grabbedHandle.item != null;
        }

        /// <summary>
        /// A disarm along a direction, for Clash of Might (a won lock, the simple variant's block at the star; runs while
        /// Disarm Strike itself is off too, as it is Clash of Might's own outcome) and Twist Disarm: the hand lets go, the
        /// weapon flies off along the direction (Fling Force), then the same no-regrab lock as every other disarm path.
        /// </summary>
        internal static bool DisarmAlong(Creature target, RagdollHand hand, Vector3 direction, string how, CollisionInstance hit = null)
        {
            if (target == null || target.isKilled || !Holds(hand))
            {
                return false;
            }
            var dropped = new List<string>();
            Release(target, hand, hit, dropped, true, direction);
            if (dropped.Count == 0)
            {
                return false;
            }
            MarkDisarmed(target, new DisarmHands(hand.side == Side.Left, hand.side == Side.Right), how, dropped);
            return true;
        }

        private static void Release(Creature target, RagdollHand hand, CollisionInstance hit, List<string> dropped, bool fling, Vector3? along = null)
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
            if (fling && (hit != null || along.HasValue) && FlingForce > 0f && item.handlers.Count == 0 && item.physicBody != null)
            {
                var direction = along.HasValue && along.Value.sqrMagnitude > 0.0001f ? along.Value.normalized
                    : hit != null && hit.impactVelocity.sqrMagnitude > 0.0001f
                    ? hit.impactVelocity.normalized
                    : (item.transform.position - target.transform.position).normalized;
                item.physicBody.AddForce(direction * FlingForce, ForceMode.VelocityChange);
                name += " (flung at " + FlingForce.ToString("0.0", CultureInfo.InvariantCulture) + " m/s)";
            }
            dropped.Add(name);
            if (item.handlers.Count == 0)
            {
                KnockedAway?.Invoke(item);
            }
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
            EndReaction(id);
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
            Reactions.Remove(id);
            BodyHits.Forget(id);
            BodyHitCreatures.Remove(id);
        }

        /// <summary>A disarmed enemy's reaction: waiting for its stagger to end, then running until EndsAt (game time).</summary>
        private readonly struct Reaction
        {
            private Reaction(Creature creature, DisarmReaction kind, float seconds, float markedAt, bool started, float endsAt)
            {
                Creature = creature;
                Kind = kind;
                Seconds = seconds;
                MarkedAt = markedAt;
                Started = started;
                EndsAt = endsAt;
            }

            public Creature Creature { get; }
            public DisarmReaction Kind { get; }
            public float Seconds { get; }
            public float MarkedAt { get; }
            public bool Started { get; }
            public float EndsAt { get; }

            public static Reaction Pending(Creature creature, DisarmReaction kind, float seconds, float now) =>
                new Reaction(creature, kind, seconds, now, false, float.PositiveInfinity);

            public Reaction Start(DisarmReaction kind, float now) => new Reaction(Creature, kind, Seconds, MarkedAt, true, now + Seconds);
        }
    }
}
