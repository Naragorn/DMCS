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
    /// Charge Stagger: a melee hit the player lands while sprinting staggers the NPC.
    /// Speed = the game's Locomotion.horizontalSpeed (m/s), fastest value in a short window before the hit.
    /// The stagger itself is the game's: Creature.ForceStagger (BrainModuleHitReaction.ForceStaggerBehaviour)
    /// with the chosen level. Its length is the game's animation; "Stagger Duration" is how long DMCS counts
    /// the NPC as staggered (Disarm Strike) and how long a knocked-down NPC is kept on the ground
    /// (Brain.AddNoStandUpModifier, which BrainModuleHitReaction.OnCycle checks before standing up).
    /// </summary>
    public static class ChargeStagger
    {
        private const string Channel = "Charge Stagger";

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ChargeStagger, true, Dmcs.Log);

        private static readonly SpeedHistory Speeds = new SpeedHistory(ChargeStaggerSettings.HistoryRetention);
        private static readonly StaggerWindows<int> Windows = new StaggerWindows<int>();
        private static readonly Dictionary<int, Creature> HeldDown = new Dictionary<int, Creature>();
        private static bool _hooked;

        /// <summary>Raised for each charge stagger (Adrenaline gains from it).</summary>
        public static event Action<Creature> Staggered;

        [ModOption(
            name = DmcsCategories.ChargeStagger + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Melee hits landed while sprinting stagger the enemy.",
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }

        [ModOption(
            name = ChargeStaggerSettings.SpeedThresholdName,
            tooltip = "Minimum running speed (m/s) for a hit to count as a charge. Walking is about 2.8, a full sprint about 6.",
            valueSourceName = nameof(SpeedThresholdValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 1)]
        [ModOptionSlider]
        public static float SpeedThreshold { get; set; } = ChargeStaggerSettings.SpeedThreshold.Default;

        [ModOption(
            name = ChargeStaggerSettings.SpeedWindowName,
            tooltip = "How far back (seconds) before the hit your top speed still counts. A charge slows down on contact.",
            valueSourceName = nameof(SpeedWindowValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 2)]
        [ModOptionSlider]
        public static float SpeedWindow { get; set; } = ChargeStaggerSettings.SpeedWindow.Default;

        [ModOption(
            name = ChargeStaggerSettings.StrengthName,
            tooltip = "How hard the enemy is staggered: the game's light, medium or full stagger, or knocked to the ground.",
            valueSourceName = nameof(StrengthValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 3)]
        public static string Strength { get; set; } = ChargeStaggerSettings.DefaultStrength;

        [ModOption(
            name = ChargeStaggerSettings.DurationName,
            tooltip = "Seconds the enemy counts as staggered (for Disarm Strike). A knocked-down enemy stays down this long.",
            valueSourceName = nameof(DurationValues),
            category = DmcsCategories.ChargeStagger,
            categoryOrder = DmcsCategories.ChargeStaggerOrder,
            order = 4)]
        [ModOptionSlider]
        public static float Duration { get; set; } = ChargeStaggerSettings.Duration.Default;

        public static ModOptionFloat[] SpeedThresholdValues() => Floats(ChargeStaggerSettings.SpeedThreshold, "0.00", " m/s");

        public static ModOptionFloat[] SpeedWindowValues() => Floats(ChargeStaggerSettings.SpeedWindow, "0.00", " s");

        public static ModOptionFloat[] DurationValues() => Floats(ChargeStaggerSettings.Duration, "0.00", " s");

        public static ModOptionString[] StrengthValues() =>
            StaggerStrengths.Names.Select(n => new ModOptionString(n, null, n)).ToArray();

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();

        /// <summary>True while the NPC is inside a charge stagger window.</summary>
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
            ReleaseAll();
        }

        /// <summary>Level unloading: nothing of this level may stay held.</summary>
        public static void ReleaseAll()
        {
            foreach (var id in Windows.TakeAll())
            {
                Release(id);
            }
            Speeds.Clear();
        }

        /// <summary>Every frame: sample the player's speed, end expired windows.</summary>
        public static void Update()
        {
            var now = Time.time;
            if (Toggle.Enabled)
            {
                var locomotion = Player.local != null ? Player.local.locomotion : null;
                if (locomotion != null)
                {
                    Speeds.Add(now, locomotion.horizontalSpeed);
                }
            }
            foreach (var id in Windows.TakeExpired(now))
            {
                Release(id);
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ReleaseAll();
            }
        }

        private static void OnCreatureHit(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            try
            {
                Handle(target, hit, eventTime);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Charge Stagger error: " + ex);
            }
        }

        private static void Handle(Creature target, CollisionInstance hit, EventTime eventTime)
        {
            if (target == null || hit == null)
            {
                return;
            }
            var now = Time.time;
            var id = target.GetInstanceID();
            var peak = Speeds.Peak(now, SpeedWindow);
            var decision = ChargeStaggerRule.Decide(new ChargeHit(
                Toggle.Enabled,
                eventTime == EventTime.OnEnd,
                SourceOf(hit),
                target.isPlayer,
                target.isKilled,
                Windows.IsStaggered(id, now),
                peak,
                SpeedThreshold));
            if (ChargeStaggerRule.IsQuiet(decision))
            {
                return;
            }
            var speedText = peak.ToString("0.00", CultureInfo.InvariantCulture) + " m/s";
            if (decision != ChargeDecision.Stagger)
            {
                Dmcs.Log.Debug(Channel, "no stagger on " + target.name + ": " + decision + " (" + speedText
                    + ", threshold " + SpeedThreshold.ToString("0.00", CultureInfo.InvariantCulture) + ")");
                return;
            }

            var strength = StaggerStrengths.Parse(Strength);
            var part = hit.damageStruct.hitRagdollPart != null ? hit.damageStruct.hitRagdollPart.type : RagdollPart.Type.Torso;
            target.ForceStagger(hit.impactVelocity, EffectOf(strength), part);
            Windows.Mark(id, now, Duration);
            if (StaggerStrengths.KnocksDown(strength) && target.brain != null)
            {
                target.brain.AddNoStandUpModifier(Toggle);
                HeldDown[id] = target;
            }
            Dmcs.Log.Debug(Channel, "staggered " + target.name + " (" + part + ") at " + speedText + ": "
                + StaggerStrengths.NameOf(strength) + ", " + Duration.ToString("0.00", CultureInfo.InvariantCulture) + " s");
            Staggered?.Invoke(target);
        }

        private static void OnCreatureDespawn(Creature creature, EventTime eventTime)
        {
            if (creature == null || eventTime != EventTime.OnStart)
            {
                return;
            }
            // Creatures are pooled: a despawned one must not come back still marked or held down.
            var id = creature.GetInstanceID();
            Windows.Remove(id);
            Release(id);
        }

        private static void Release(int id)
        {
            if (!HeldDown.TryGetValue(id, out var creature))
            {
                return;
            }
            HeldDown.Remove(id);
            if (creature != null && creature.brain != null)
            {
                creature.brain.RemoveNoStandUpModifier(Toggle);
            }
        }

        private static BrainModuleHitReaction.PushBehaviour.Effect EffectOf(StaggerStrength strength)
        {
            switch (strength)
            {
                case StaggerStrength.Light: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerLight;
                case StaggerStrength.Medium: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerMedium;
                case StaggerStrength.KnockDown: return BrainModuleHitReaction.PushBehaviour.Effect.Destabilize;
                default: return BrainModuleHitReaction.PushBehaviour.Effect.StaggerFull;
            }
        }

        /// <summary>
        /// Who dealt the hit. A held item: Item.handlers holds the hands holding it (thrown items have
        /// none, telekinesis is tkHandlers). A body part: the damager's collision handler is a ragdoll part.
        /// Spells: CollisionInstance.casterHand.
        /// </summary>
        private static HitSource SourceOf(CollisionInstance hit)
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
                return item.lastHandler != null && item.lastHandler.creature == player ? HitSource.PlayerNotMelee : HitSource.NotPlayer;
            }
            var part = handler != null ? handler.ragdollPart : null;
            if (part != null && part.ragdoll != null && part.ragdoll.creature == player)
            {
                return HitSource.PlayerBody;
            }
            if (hit.casterHand != null && hit.casterHand.mana != null && hit.casterHand.mana.creature == player)
            {
                return HitSource.PlayerNotMelee;
            }
            return HitSource.NotPlayer;
        }
    }
}
