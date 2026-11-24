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
    /// Counter Parry and Power Parry (spec section 6, built 2026-11-20). On the game's parry event (an NPC attack stopped
    /// by the player's held weapon, EventManager.onCreatureAttackParry) DMCS reads how fast the parrying hand's controller
    /// moved just before (HandSpeeds): a guard brought up in time staggers the enemy (a counter); a hard swing into the
    /// attack also knocks its weapon away (Disarm Strike's path). A held guard stays the game's own parry.
    /// Subscribed after Clash of Might (DmcsScript starts it later), so a clash that took this parry is already known.
    /// The weapon is knocked away at once; the stagger waits for the next frame, because right after this event the
    /// game's BrainModuleMelee pushes the NPC itself (TryPush Parry: recoil or a riposte), which would override it.
    /// </summary>
    public static class Parry
    {
        private const string Channel = "Parry";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.Parry, ParrySettings.DefaultEnabled, Dmcs.Log);

        private static readonly StaggerWindows<int> Windows = new StaggerWindows<int>();
        private static readonly List<(Creature Npc, Vector3 Direction)> PendingStaggers = new List<(Creature Npc, Vector3 Direction)>();
        private static readonly System.Random Dice = new System.Random();
        private static bool _hooked;

        [ModOption(
            name = ParrySettings.EnabledName,
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Parrying an enemy's attack at the right moment or with force pays off: Counter Parry and Power Parry below.",
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.MenuEnabled;
            set => Toggle.Set(value);
        }

        [ModOption(
            name = ParrySettings.CounterName,
            tooltip = "Bring your weapon into the enemy's swing just in time (not held there all along) and the enemy staggers.",
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 1)]
        public static bool CounterEnabled { get; set; } = ParrySettings.DefaultCounter;

        [ModOption(
            name = ParrySettings.CounterSpeedName,
            tooltip = "How fast (m/s) your hand must move just before the parry for a counter. Slower: an ordinary parry.",
            valueSourceName = nameof(CounterSpeedValues),
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 2)]
        [ModOptionSlider]
        public static float CounterSpeed { get; set; } = ParrySettings.CounterSpeed.Default;

        [ModOption(
            name = ParrySettings.StaggerDurationName,
            tooltip = "Seconds a countered enemy counts as staggered: time for a hand hit or a twist to disarm it.",
            valueSourceName = nameof(StaggerDurationValues),
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 3)]
        [ModOptionSlider]
        public static float StaggerDuration { get; set; } = ParrySettings.StaggerDuration.Default;

        [ModOption(
            name = ParrySettings.PowerName,
            tooltip = "Swing hard into the enemy's attack and its weapon flies out of its hand.",
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 4)]
        public static bool PowerEnabled { get; set; } = ParrySettings.DefaultPower;

        [ModOption(
            name = ParrySettings.PowerSpeedName,
            tooltip = "How fast (m/s) your hand must swing into the attack for a power parry.",
            valueSourceName = nameof(PowerSpeedValues),
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 5)]
        [ModOptionSlider]
        public static float PowerSpeed { get; set; } = ParrySettings.PowerSpeed.Default;

        [ModOption(
            name = ParrySettings.PowerChanceName,
            tooltip = "Chance (percent) that a power parry knocks the weapon away.",
            valueSourceName = nameof(PowerChanceValues),
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 6)]
        [ModOptionSlider]
        public static float PowerChance { get; set; } = ParrySettings.PowerChance.Default;

        [ModOption(
            name = ParrySettings.SpeedWindowName,
            tooltip = "Seconds before the parry in which your hand's fastest speed counts.",
            valueSourceName = nameof(SpeedWindowValues),
            category = DmcsCategories.Parry,
            categoryOrder = DmcsCategories.ParryOrder,
            order = 7)]
        [ModOptionSlider]
        public static float SpeedWindow { get; set; } = ParrySettings.SpeedWindow.Default;

        public static ModOptionFloat[] CounterSpeedValues() => Floats(ParrySettings.CounterSpeed, "0.00", " m/s");

        public static ModOptionFloat[] StaggerDurationValues() => Floats(ParrySettings.StaggerDuration, "0.00", " s");

        public static ModOptionFloat[] PowerSpeedValues() => Floats(ParrySettings.PowerSpeed, "0.00", " m/s");

        public static ModOptionFloat[] PowerChanceValues() => Floats(ParrySettings.PowerChance, "0", " %");

        public static ModOptionFloat[] SpeedWindowValues() => Floats(ParrySettings.SpeedWindow, "0.00", " s");

        /// <summary>Inside a counter's stagger window (HitInfo.IsStaggered counts it, as a Charge Stagger window).</summary>
        public static bool IsStaggered(Creature creature)
        {
            return creature != null && Windows.IsStaggered(creature.GetInstanceID(), Time.time);
        }

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (!_hooked)
            {
                EventManager.onCreatureAttackParry += OnParry;
                EventManager.onCreatureDespawn += OnCreatureDespawn;
                _hooked = true;
            }
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureAttackParry -= OnParry;
                EventManager.onCreatureDespawn -= OnCreatureDespawn;
                _hooked = false;
            }
            ResetAll();
        }

        /// <summary>Feature off or level unloading.</summary>
        public static void ResetAll()
        {
            Windows.TakeAll();
            PendingStaggers.Clear();
        }

        /// <summary>Every frame: the counters' staggers, after the game's own parry push; ended windows.</summary>
        public static void Update()
        {
            if (PendingStaggers.Count > 0)
            {
                foreach (var (npc, direction) in PendingStaggers.ToList())
                {
                    if (npc != null && !npc.isKilled)
                    {
                        npc.ForceStagger(direction, BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium);
                    }
                }
                PendingStaggers.Clear();
            }
            Windows.TakeExpired(Time.time);
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ResetAll();
            }
        }

        private static void OnCreatureDespawn(Creature creature, EventTime eventTime)
        {
            if (creature != null && eventTime == EventTime.OnStart)
            {
                // Creatures are pooled: a despawned one must not come back still staggered.
                Windows.Remove(creature.GetInstanceID());
            }
        }

        private static void OnParry(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            try
            {
                Handle(parried, parriedItem, parrying, parryingItem);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Parry error: " + ex);
            }
        }

        private static void Handle(Creature npc, Item npcItem, Creature player, Item playerItem)
        {
            var playerHand = playerItem != null ? playerItem.mainHandler : null;
            var npcHand = npcItem != null ? npcItem.mainHandler : null;
            var playerParry = npc != null && !npc.isPlayer && !npc.isKilled && player != null && player.isPlayer
                && playerHand != null && playerHand.creature != null && playerHand.creature.isPlayer;
            var speed = playerHand != null ? HandSpeeds.Peak(playerHand.side, SpeedWindow) : 0f;
            var verdict = ParryRule.Decide(new ParryContact(
                Toggle.Enabled,
                CounterEnabled,
                PowerEnabled,
                playerParry,
                npc != null && (ClashOfMight.IsLockedWith(npc) || DisarmStrike.IsDisarmed(npc)),
                npcHand != null && npcHand.creature == npc,
                DisarmShield.Keeps(DisarmStrike.IsShieldItem(npcItem), DisarmStrike.ShieldsCanBeDisarmed),
                speed,
                CounterSpeed,
                PowerSpeed,
                (float)Dice.NextDouble(),
                PowerChance / 100f));
            if (ParryRule.IsQuiet(verdict.Decision))
            {
                return;
            }
            var facts = "hand " + F(speed) + " m/s (counter from " + F(CounterSpeed) + ", power from " + F(PowerSpeed) + ")";
            var what = npc.name + "'s " + (npcItem != null && npcItem.data != null ? npcItem.data.id : "weapon");
            if (verdict.Disarms)
            {
                var along = ClashOfMight.AwayFromPlayer(npc);
                if (DisarmStrike.DisarmAlong(npc, npcHand, along, "power parry, " + facts))
                {
                    Dmcs.Log.Debug(Channel, "power parry on " + what + ": knocked away (" + facts + ")");
                }
            }
            else if (verdict.Decision != ParryDecision.Counter)
            {
                Dmcs.Log.Debug(Channel, "parry of " + what + ": " + (verdict.Decision == ParryDecision.TooSlow ? "an ordinary parry, TooSlow" : verdict.Decision.ToString()) + " (" + facts + ")");
            }
            if (verdict.Staggers)
            {
                Windows.Mark(npc.GetInstanceID(), Time.time, StaggerDuration);
                PendingStaggers.Add((npc, ClashOfMight.AwayFromPlayer(npc)));
                Dmcs.Log.Debug(Channel, "counter parry on " + npc.name + ": staggered for " + F(StaggerDuration) + " s (" + facts + ")");
            }
        }

        private static string F(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();
    }
}
