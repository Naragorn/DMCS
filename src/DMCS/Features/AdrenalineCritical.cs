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
    /// Adrenaline steps 4b/4c: the critical (Dark Messiah's fatality), the hit the full meter waits for.
    /// Time: the game's TimeManager.SetSlowMotion, with the Hyperfocus skill's player buffs in Hyperfocus mode
    /// (SpellPowerSlowTime.BuffPlayer: Creature.AddJointForceMultiplier, Locomotion.SetSpeedModifier/SetPhysicModifier).
    /// Chest stab: BrainModuleDeath picks its dying clip from stabAnimationData/defaultAnimationData synchronously
    /// inside Creature.Kill (TryPlayAnimation → DyingCoroutine → AnimationData.Pick before its first yield), so DMCS
    /// gives that NPC a kneeling-only copy for the kill and puts the game's data back right after.
    /// </summary>
    public static class AdrenalineCritical
    {
        private const string Channel = "Adrenaline";
        private const string StabDeathsId = "HumanStabDeaths";

        /// <summary>Handler key for the game's modifier lists (joint force, speed, physics).</summary>
        private static readonly object Handler = new object();

        private const string KneelAnimationId = "HumanIdleExamineKneel";

        private static readonly Dictionary<int, SwappedDeath> Swapped = new Dictionary<int, SwappedDeath>();

        /// <summary>Enemies dying on their knees after a chest stab: kick or behead them until the time.</summary>
        private static readonly Dictionary<int, Victim> Finishers = new Dictionary<int, Victim>();

        /// <summary>Enemies kneeling alive (step 4c): any hit executes them until the time.</summary>
        private static readonly Dictionary<int, Victim> Kneeling = new Dictionary<int, Victim>();

        private static readonly System.Random Dice = new System.Random();

        /// <summary>Cut-off heads waiting for the game's cut to finish before they get their push.</summary>
        private static readonly List<HeadPush> FlyingHeads = new List<HeadPush>();


        private static bool _timeOwned;
        private static bool _playerBuffed;
        private static float _timeScale;
        private static float _timeEndsAt;

        [ModOption(name = AdrenalineSettings.CriticalTimeModeName, tooltip = "Slow-motion of the critical. Hyperfocus: the world slows, you keep moving fast (the game's Hyperfocus). Slow Time: everything slows, you too (the game's slow time).", valueSourceName = nameof(TimeModeValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 12)]
        public static string TimeMode { get; set; } = AdrenalineSettings.DefaultTimeMode;

        [ModOption(name = AdrenalineSettings.HyperfocusScaleName, tooltip = "World speed during a Hyperfocus critical (the game's Hyperfocus: 0.1).", valueSourceName = nameof(HyperfocusScaleValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 13)]
        [ModOptionSlider]
        public static float HyperfocusScale { get; set; } = AdrenalineSettings.HyperfocusScale.Default;

        [ModOption(name = AdrenalineSettings.HyperfocusDurationName, tooltip = "Real seconds a Hyperfocus critical lasts.", valueSourceName = nameof(HyperfocusDurationValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 14)]
        [ModOptionSlider]
        public static float HyperfocusDuration { get; set; } = AdrenalineSettings.HyperfocusDuration.Default;

        [ModOption(name = AdrenalineSettings.SlowTimeScaleName, tooltip = "World and player speed during a Slow Time critical (the game's slow time: 0.5).", valueSourceName = nameof(SlowTimeScaleValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 15)]
        [ModOptionSlider]
        public static float SlowTimeScale { get; set; } = AdrenalineSettings.SlowTimeScale.Default;

        [ModOption(name = AdrenalineSettings.SlowTimeDurationName, tooltip = "Real seconds a Slow Time critical lasts.", valueSourceName = nameof(SlowTimeDurationValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 16)]
        [ModOptionSlider]
        public static float SlowTimeDuration { get; set; } = AdrenalineSettings.SlowTimeDuration.Default;

        [ModOption(name = AdrenalineSettings.NeedsHardSwingName, tooltip = "Only a hard hit is the critical; lighter hits are normal hits.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 17)]
        public static bool NeedsHardSwing { get; set; } = AdrenalineSettings.DefaultNeedsHardSwing;

        [ModOption(name = AdrenalineSettings.MinSwingSpeedName, tooltip = "How fast (m/s) the weapon, fist or kick must hit to be the critical.", valueSourceName = nameof(MinSwingSpeedValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 18)]
        [ModOptionSlider]
        public static float MinSwingSpeed { get; set; } = AdrenalineSettings.MinSwingSpeed.Default;

        [ModOption(name = AdrenalineSettings.RangedCountsName, tooltip = "Thrown weapons and spells can be the critical too.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 19)]
        public static bool RangedCounts { get; set; } = AdrenalineSettings.DefaultRangedCounts;

        [ModOption(name = AdrenalineSettings.KillsName, tooltip = "The critical always kills, like Dark Messiah's fatality.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 20)]
        public static bool Kills { get; set; } = AdrenalineSettings.DefaultKills;

        [ModOption(name = AdrenalineSettings.DismemberName, tooltip = "The critical cuts off the hit head, arm or leg (where the game allows it).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 21)]
        public static bool Dismember { get; set; } = AdrenalineSettings.DefaultDismember;

        [ModOption(name = AdrenalineSettings.KnockbackName, tooltip = "How hard (m/s) the critical throws the enemy along the hit. 0 = no throw.", valueSourceName = nameof(KnockbackValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 22)]
        [ModOptionSlider]
        public static float Knockback { get; set; } = AdrenalineSettings.Knockback.Default;

        [ModOption(name = AdrenalineSettings.BattleCryName, tooltip = "You shout on the critical (your character's own attack shout).", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 23)]
        public static bool BattleCry { get; set; } = AdrenalineSettings.DefaultBattleCry;

        [ModOption(name = AdrenalineSettings.ChestStabKneelsName, tooltip = "A critical stab into the chest makes the enemy sink to its knees and die. Kick it away or let it fall.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 24)]
        public static bool ChestStabKneels { get; set; } = AdrenalineSettings.DefaultChestStabKneels;

        [ModOption(name = AdrenalineSettings.ExecutionWindowName, tooltip = "Seconds a kneeling enemy waits for you: after a chest stab, kick it away or behead it; after it went down on its own, any hit executes it.", valueSourceName = nameof(ExecutionWindowValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 25)]
        [ModOptionSlider]
        public static float ExecutionWindow { get; set; } = AdrenalineSettings.ExecutionWindow.Default;

        [ModOption(name = AdrenalineSettings.KneelEnabledName, tooltip = "With a full meter, the enemy goes down on its knees when you land no hit for a while or only light hits.", category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 26)]
        public static bool KneelEnabled { get; set; } = AdrenalineSettings.DefaultKneelEnabled;

        [ModOption(name = AdrenalineSettings.KneelAfterNoHitName, tooltip = "Seconds without a hit (meter full) before the enemy kneels.", valueSourceName = nameof(KneelAfterNoHitValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 27)]
        [ModOptionSlider]
        public static float KneelAfterNoHit { get; set; } = AdrenalineSettings.KneelAfterNoHit.Default;

        [ModOption(name = AdrenalineSettings.KneelAfterLightHitsName, tooltip = "Light hits (meter full) after which the enemy kneels.", valueSourceName = nameof(LightHitValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 28)]
        [ModOptionSlider]
        public static int KneelAfterLightHits { get; set; } = AdrenalineSettings.DefaultKneelAfterLightHits;

        [ModOption(name = AdrenalineSettings.KneelDeathChanceName, tooltip = "Chance (%) that a kneeling enemy you did not execute dies instead of standing up.", valueSourceName = nameof(KneelDeathChanceValues), category = DmcsCategories.Adrenaline, categoryOrder = DmcsCategories.AdrenalineOrder, order = 29)]
        [ModOptionSlider]
        public static int KneelDeathChance { get; set; } = AdrenalineSettings.DefaultKneelDeathChance;

        public static ModOptionFloat[] ExecutionWindowValues() => Floats(AdrenalineSettings.ExecutionWindow, "0", " s");

        public static ModOptionFloat[] KneelAfterNoHitValues() => Floats(AdrenalineSettings.KneelAfterNoHit, "0.0", " s");

        public static ModOptionInt[] LightHitValues() =>
            AdrenalineSettings.LightHitValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture), v)).ToArray();

        public static ModOptionInt[] KneelDeathChanceValues() =>
            DisarmStrikeSettings.ChanceValues().Select(v => new ModOptionInt(v.ToString(CultureInfo.InvariantCulture) + " %", v)).ToArray();

        public static ModOptionString[] TimeModeValues() => CriticalTimeModes.Names.Select(n => new ModOptionString(n, null, n)).ToArray();

        public static ModOptionFloat[] HyperfocusScaleValues() => Floats(AdrenalineSettings.HyperfocusScale, "0.00", "×");

        public static ModOptionFloat[] HyperfocusDurationValues() => Floats(AdrenalineSettings.HyperfocusDuration, "0.0", " s");

        public static ModOptionFloat[] SlowTimeScaleValues() => Floats(AdrenalineSettings.SlowTimeScale, "0.00", "×");

        public static ModOptionFloat[] SlowTimeDurationValues() => Floats(AdrenalineSettings.SlowTimeDuration, "0.0", " s");

        public static ModOptionFloat[] MinSwingSpeedValues() => Floats(AdrenalineSettings.MinSwingSpeed, "0.00", " m/s");

        public static ModOptionFloat[] KnockbackValues() => Floats(AdrenalineSettings.Knockback, "0", " m/s");

        /// <summary>Whether this hit is the critical (the meter's state is passed in).</summary>
        public static CriticalDecision Decide(bool enabled, bool ready, Creature target, CollisionInstance hit, HitSource source)
        {
            return CriticalRule.Decide(new CriticalHit(
                enabled, ready, source, RangedCounts, target.isPlayer, target.isKilled,
                NeedsHardSwing, hit.impactVelocity.magnitude, MinSwingSpeed));
        }

        private static bool IsStab(CollisionInstance hit) =>
            hit.damageStruct.penetration != DamageStruct.Penetration.None || hit.damageStruct.damageType == DamageType.Pierce;

        public static bool IsKneeling(Creature target) => target != null && Kneeling.ContainsKey(target.GetInstanceID());

        public static bool IsFinishable(Creature target) => target != null && Finishers.ContainsKey(target.GetInstanceID());

        public static bool IsChestStab(CollisionInstance hit)
        {
            var part = hit.damageStruct.hitRagdollPart;
            return ChestStabKneels && CriticalRule.IsChestStab(
                part != null && part.type == RagdollPart.Type.Torso,
                hit.damageStruct.penetration != DamageStruct.Penetration.None,
                hit.damageStruct.damageType == DamageType.Pierce);
        }

        /// <summary>
        /// At the hit's start, before the game's damage may kill: a chest-stab critical gets the kneeling deaths,
        /// so the kneel also happens when the stab's own damage kills.
        /// </summary>
        public static void Prepare(Creature target, CollisionInstance hit)
        {
            if (IsChestStab(hit) || (IsKneeling(target) && IsStab(hit)))
            {
                SwapToKneel(target);
            }
        }

        /// <summary>At the hit's end: the critical itself.</summary>
        public static void Execute(Creature target, CollisionInstance hit, HitSource source, bool stabKneels = false)
        {
            var part = hit.damageStruct.hitRagdollPart;
            var partName = part != null ? part.type.ToString() : "?";
            Dmcs.Log.Debug(Channel, "CRITICAL on " + target.name + " (" + partName + ", " + source + ", swing "
                + hit.impactVelocity.magnitude.ToString("0.00", CultureInfo.InvariantCulture) + " m/s)");
            if (BattleCry)
            {
                Shout();
            }
            if (IsChestStab(hit) || (stabKneels && IsStab(hit)))
            {
                // The slow-motion waits for the finish: the kick or the beheading.
                ChestStab(target, hit);
                return;
            }
            StartTime();
            Restore(target);
            if (Dismember && part != null)
            {
                var cut = part.SafeSlice();
                Dmcs.Log.Debug(Channel, cut ? "cut off " + partName : "no cut (" + partName + ": the game refused)");
            }
            if (Kills && !target.isKilled)
            {
                target.Kill(hit);
                Dmcs.Log.Debug(Channel, "killed " + target.name);
            }
            Throw(target, hit);
        }

        public static void Update()
        {
            var now = Time.unscaledTime;
            if (_timeOwned && now >= _timeEndsAt)
            {
                EndTime(force: false);
            }
            for (var i = FlyingHeads.Count - 1; i >= 0; i--)
            {
                var push = FlyingHeads[i];
                if (push.Head == null || now >= push.GiveUpAt)
                {
                    FlyingHeads.RemoveAt(i);
                    continue;
                }
                if (push.Head.isSliced)
                {
                    push.Head.physicBody?.AddForce(push.Velocity, ForceMode.VelocityChange);
                    FlyingHeads.RemoveAt(i);
                    Dmcs.Log.Debug(Channel, "the head flies");
                }
            }
            foreach (var id in Finishers.Where(f => now >= f.Value.Until || f.Value.Creature == null).Select(f => f.Key).ToList())
            {
                Finishers.Remove(id);
            }
            // As BrainModuleFear.OnUpdate keeps a cowering NPC's animation: the creature's own UpdateDynamicAnimation
            // clears the flag when the animator is not busy for a frame, while the loop still runs.
            foreach (var victim in Kneeling.Values)
            {
                var c = victim.Creature;
                if (c != null && !c.isKilled && !c.isPlayingDynamicAnimation && c.animator != null && c.animator.GetBool(Creature.hashDynamicLoop3))
                {
                    c.isPlayingDynamicAnimation = true;
                }
            }
            foreach (var pair in Kneeling.Where(k => now >= k.Value.Until || k.Value.Creature == null || k.Value.Creature.isKilled).ToList())
            {
                EndKneel(pair.Key, pair.Value);
            }
        }

        /// <summary>A hit on an enemy dying on its knees after a chest stab: kick it away or behead it (slow-motion).</summary>
        public static bool TryFinish(Creature target, CollisionInstance hit, HitSource source)
        {
            if (!Finishers.TryGetValue(target.GetInstanceID(), out var victim))
            {
                return false;
            }
            var part = hit.damageStruct.hitRagdollPart;
            var headOrNeck = part != null && (part.type == RagdollPart.Type.Head || part.type == RagdollPart.Type.Neck);
            var move = FinishRule.Decide(Time.unscaledTime < victim.Until, source, HitInfo.IsByFootOrLeg(hit), headOrNeck,
                hit.damageStruct.damageType == DamageType.Slash);
            if (move == FinishMove.None)
            {
                return false;
            }
            Finishers.Remove(target.GetInstanceID());
            if (move == FinishMove.Kick)
            {
                StartTime();
                Throw(target, hit);
                Dmcs.Log.Debug(Channel, "kicked " + target.name + " away");
                return true;
            }
            // Ragdoll.TrySlice only starts the cut (SliceCoroutine sets isSliced later): the push waits for it in Update.
            var head = target.ragdoll != null ? target.ragdoll.headPart : null;
            var cut = head != null && (head.isSliced || head.SafeSlice());
            if (!cut)
            {
                Dmcs.Log.Debug(Channel, "no beheading on " + target.name + ": the game refused the cut");
                return true;
            }
            StartTime();
            if (Knockback > 0f)
            {
                FlyingHeads.Add(new HeadPush(head, Direction(target, hit) * Knockback, Time.unscaledTime + 1f));
            }
            Dmcs.Log.Debug(Channel, "beheaded " + target.name);
            return true;
        }

        /// <summary>
        /// Step 4c: the enemy goes down on its knees, alive. Held the way the game's own BrainModuleFear.Panic holds a
        /// cowering NPC: brain incapacitated, behaviour tree reset and paused, stance idle, then the animation
        /// (a running behaviour tree stops dynamic animations, e.g. BrainModuleMove.StopAnimation while moving).
        /// </summary>
        public static bool KneelAlive(Creature target, KneelReason reason)
        {
            if (target == null || target.isKilled || target.brain == null || target.brain.instance == null || IsKneeling(target))
            {
                return false;
            }
            var brain = target.brain;
            brain.isIncapacitated = true;
            brain.instance.tree.Reset();
            brain.instance.updateTree = false;
            brain.instance.GetModule<BrainModuleStance>(false)?.SetStance(BrainModuleStance.Stance.Idle);
            target.StopAnimation(true);
            var data = Catalog.GetData<AnimationData>(KneelAnimationId, false);
            if (data == null || data.animationClips.Any(c => c.animationClip == null))
            {
                Dmcs.Log.Debug(Channel, "kneel animation '" + KneelAnimationId + "' not loaded: "
                    + (data == null ? "no catalog entry" : string.Join(", ", data.animationClips.Select(c => c.address + "=" + (c.animationClip != null ? "ok" : "null")))));
            }
            target.PlayAnimation(KneelAnimationId);
            Kneeling[target.GetInstanceID()] = new Victim(target, Time.unscaledTime + ExecutionWindow);
            Dmcs.Log.Debug(Channel, target.name + " kneels (" + reason + "): execute within "
                + ExecutionWindow.ToString("0", CultureInfo.InvariantCulture) + " s (dynamic animation "
                + (target.isPlayingDynamicAnimation ? "playing" : "NOT playing") + "; clips "
                + (data != null ? string.Join(", ", data.animationClips.Select(c => c.step + " " + (c.animationClip != null
                    ? c.animationClip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "null"))) : "-") + ")");
            return true;
        }

        /// <summary>As BrainModuleFear.StopPanic: the brain runs again, the kneeling animation ends.</summary>
        private static void Release(Creature creature)
        {
            if (creature == null || creature.brain == null)
            {
                return;
            }
            creature.brain.isIncapacitated = false;
            if (creature.brain.instance != null)
            {
                creature.brain.instance.updateTree = true;
            }
            if (!creature.isKilled)
            {
                creature.StopAnimation();
            }
        }

        /// <summary>Any player hit on a kneeling enemy is the execution: the critical (a stab kneels it into death).</summary>
        public static bool TryExecute(Creature target, CollisionInstance hit, HitSource source)
        {
            var id = target.GetInstanceID();
            if (source == HitSource.NotPlayer || !Kneeling.TryGetValue(id, out var victim))
            {
                return false;
            }
            Kneeling.Remove(id);
            Release(target);
            Dmcs.Log.Debug(Channel, "execution of " + target.name);
            Execute(target, hit, source, stabKneels: true);
            return true;
        }

        private static void EndKneel(int id, Victim victim)
        {
            Kneeling.Remove(id);
            var creature = victim.Creature;
            if (creature == null || creature.isKilled)
            {
                return;
            }
            Release(creature);
            if (KneelOutcome.AfterWindow((float)Dice.NextDouble(), KneelDeathChance / 100f) == KneelEnd.Dies)
            {
                creature.Kill();
                Dmcs.Log.Debug(Channel, creature.name + " was not executed and dies");
                return;
            }
            Dmcs.Log.Debug(Channel, creature.name + " was not executed and stands up");
        }

        /// <summary>Feature off, level unload, script stop: time back, buffs off, every NPC's death data back.</summary>
        public static void StopAll()
        {
            EndTime(force: true);
            foreach (var id in Swapped.Keys.ToList())
            {
                Restore(id);
            }
            Finishers.Clear();
            FlyingHeads.Clear();
            foreach (var pair in Kneeling.ToList())
            {
                Kneeling.Remove(pair.Key);
                Release(pair.Value.Creature);
            }
        }

        private static void ChestStab(Creature target, CollisionInstance hit)
        {
            if (!Swapped.ContainsKey(target.GetInstanceID()))
            {
                SwapToKneel(target);
            }
            if (!target.isKilled)
            {
                target.Kill(hit);
            }
            var picked = Swapped.TryGetValue(target.GetInstanceID(), out var swap)
                ? swap.Kneel.animationClips.FirstOrDefault(c => c.lastPicked)
                : null;
            Dmcs.Log.Debug(Channel, "chest-stab fatality: " + target.name + " kneels ("
                + (picked != null ? picked.address : "no kneeling clip picked: the game played none") + "), kick or behead within "
                + ExecutionWindow.ToString("0", CultureInfo.InvariantCulture) + " s");
            Restore(target);
            Finishers[target.GetInstanceID()] = new Victim(target, Time.unscaledTime + ExecutionWindow);
        }

        private static void SwapToKneel(Creature target)
        {
            var death = target.brain?.instance?.GetModule<BrainModuleDeath>(false);
            if (death == null)
            {
                Dmcs.Log.Debug(Channel, "no kneel: " + target.name + " has no death module");
                return;
            }
            var id = target.GetInstanceID();
            if (Swapped.ContainsKey(id))
            {
                return;
            }
            var source = death.stabAnimationData ?? Catalog.GetData<AnimationData>(StabDeathsId, false);
            var kneel = KneelData(source);
            if (kneel == null)
            {
                Dmcs.Log.Debug(Channel, "no kneel: no kneeling clips in " + (source != null ? source.id : "null"));
                return;
            }
            Swapped[id] = new SwappedDeath(death, death.stabAnimationData, death.defaultAnimationData, kneel);
            death.stabAnimationData = kneel;
            death.defaultAnimationData = kneel;
        }

        private static AnimationData KneelData(AnimationData source)
        {
            if (source?.animationClips == null)
            {
                return null;
            }
            var kneel = new AnimationData
            {
                id = "DMCS.Kneel",
                useDeathCurve = source.useDeathCurve,
                allowRootMotion = source.allowRootMotion,
            };
            var addresses = source.animationClips.Select(c => c.address).ToList();
            foreach (DeathDirection direction in Enum.GetValues(typeof(DeathDirection)))
            {
                foreach (var address in KneelClips.For(direction, addresses))
                {
                    var original = source.animationClips.First(c => c.address == address);
                    if (original.animationClip == null)
                    {
                        continue;
                    }
                    kneel.animationClips.Add(new AnimationData.Clip
                    {
                        address = original.address,
                        animationClip = original.animationClip,
                        step = original.step,
                        animationSpeed = original.animationSpeed,
                        moveDirection = ToClipDirection(direction),
                        aimDirection = original.aimDirection,
                        weight = original.weight,
                        chance = original.chance,
                        container = kneel,
                    });
                }
            }
            return kneel.animationClips.Count > 0 ? kneel : null;
        }

        private static AnimationData.Clip.Direction ToClipDirection(DeathDirection direction)
        {
            switch (direction)
            {
                case DeathDirection.Forward: return AnimationData.Clip.Direction.Forward;
                case DeathDirection.Backward: return AnimationData.Clip.Direction.Backward;
                case DeathDirection.Left: return AnimationData.Clip.Direction.Left;
                default: return AnimationData.Clip.Direction.Right;
            }
        }

        private static void Restore(Creature target)
        {
            if (target != null)
            {
                Restore(target.GetInstanceID());
            }
        }

        private static void Restore(int id)
        {
            if (!Swapped.TryGetValue(id, out var swap))
            {
                return;
            }
            Swapped.Remove(id);
            if (swap.Module != null)
            {
                swap.Module.stabAnimationData = swap.Stab;
                swap.Module.defaultAnimationData = swap.Default;
            }
        }

        /// <summary>Along the hit: a dying NPC first leaves its dying animation (the game's own StopDying), then every part gets the push.</summary>
        private static void Throw(Creature target, CollisionInstance hit)
        {
            if (Knockback <= 0f || target.ragdoll == null)
            {
                return;
            }
            var direction = Direction(target, hit);
            var death = target.brain?.instance?.GetModule<BrainModuleDeath>(false);
            if (target.isKilled && death != null)
            {
                death.StopDying();
            }
            else if (!target.isKilled)
            {
                target.ragdoll.SetState(Ragdoll.State.Destabilized);
            }
            foreach (var p in target.ragdoll.parts)
            {
                p.physicBody?.AddForce(direction * Knockback, ForceMode.VelocityChange);
            }
            Dmcs.Log.Debug(Channel, "threw " + target.name + " at " + Knockback.ToString("0", CultureInfo.InvariantCulture) + " m/s");
        }

        private static Vector3 Direction(Creature target, CollisionInstance hit) =>
            hit.impactVelocity.sqrMagnitude > 0.0001f
                ? hit.impactVelocity.normalized
                : (target.transform.position - Player.local.transform.position).normalized;

        private static void Shout()
        {
            var player = Player.currentCreature;
            var speak = player?.brain?.instance?.GetModule<BrainModuleSpeak>(false);
            if (speak == null)
            {
                Dmcs.Log.Debug(Channel, "battle cry: the player has no voice module");
                return;
            }
            BrainModuleSpeak.TrySpeak report = null;
            report = (abnormal, hash, dialog, clip, clipHash, status) =>
            {
                speak.OnTrySpeak -= report;
                Dmcs.Log.Debug(Channel, "battle cry: " + status + (clip != null ? " (" + clip.name + ")" : ""));
            };
            speak.OnTrySpeak += report;
            speak.Play("attackmelee", false, false);
        }

        private static void StartTime()
        {
            if (_timeOwned)
            {
                EndTime(force: true);
            }
            if (TimeManager.slowMotionState != TimeManager.SlowMotionState.Disabled)
            {
                Dmcs.Log.Debug(Channel, "no slow-motion: another one runs (" + TimeManager.slowMotionState + ")");
                return;
            }
            var hyperfocus = CriticalTimeModes.BuffsPlayer(TimeMode);
            _timeScale = hyperfocus ? HyperfocusScale : SlowTimeScale;
            var duration = hyperfocus ? HyperfocusDuration : SlowTimeDuration;
            var effect = Catalog.GetData<EffectData>(hyperfocus ? "Hyperfocus" : "SpellSlowTime", false);
            TimeManager.SetSlowMotion(true, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.25f, 1f), effect);
            _timeOwned = true;
            _timeEndsAt = Time.unscaledTime + duration;
            var player = Player.currentCreature;
            if (hyperfocus && player != null && player.currentLocomotion != null)
            {
                var (joint, speed, drag) = CriticalTimeModes.Buffs(_timeScale);
                player.AddJointForceMultiplier(Handler, joint, joint);
                player.currentLocomotion.SetSpeedModifier(Handler, speed, speed, speed, speed, 2f, speed);
                player.currentLocomotion.SetPhysicModifier(Handler, null, -1f, drag);
                _playerBuffed = true;
            }
            Dmcs.Log.Debug(Channel, "slow-motion " + TimeMode + ": " + _timeScale.ToString("0.00", CultureInfo.InvariantCulture)
                + "× for " + duration.ToString("0.0", CultureInfo.InvariantCulture) + " s" + (_playerBuffed ? ", you keep your speed" : ""));
        }

        private static void EndTime(bool force)
        {
            if (_playerBuffed)
            {
                _playerBuffed = false;
                var player = Player.currentCreature;
                if (player != null)
                {
                    player.RemoveJointForceMultiplier(Handler);
                    if (player.currentLocomotion != null)
                    {
                        player.currentLocomotion.RemoveSpeedModifier(Handler);
                        player.currentLocomotion.RemovePhysicModifier(Handler);
                        // As the game's Hyperfocus does when it ends (SpellPowerSlowTime.DebuffPlayer, maxSpeedCap 10).
                        player.currentLocomotion.velocity = Vector3.ClampMagnitude(player.currentLocomotion.velocity, 10f);
                    }
                }
            }
            if (!_timeOwned)
            {
                return;
            }
            _timeOwned = false;
            var playerDead = Player.currentCreature == null || Player.currentCreature.isKilled;
            // Only end our own slow-motion: the player's Focus or the game's death slow-motion may have taken over.
            if (playerDead || (!force && Mathf.Abs(Time.timeScale - _timeScale) > 0.01f))
            {
                Dmcs.Log.Debug(Channel, "slow-motion over; another slow-motion took over, left running");
                return;
            }
            TimeManager.SetSlowMotion(false, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.25f, 1f));
            Dmcs.Log.Debug(Channel, "slow-motion over: time back to normal");
        }

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();

        private sealed class HeadPush
        {
            public HeadPush(RagdollPart head, Vector3 velocity, float giveUpAt)
            {
                Head = head;
                Velocity = velocity;
                GiveUpAt = giveUpAt;
            }

            public RagdollPart Head { get; }
            public Vector3 Velocity { get; }
            public float GiveUpAt { get; }
        }

        private sealed class Victim
        {
            public Victim(Creature creature, float until)
            {
                Creature = creature;
                Until = until;
            }

            public Creature Creature { get; }
            public float Until { get; }
        }

        private sealed class SwappedDeath
        {
            public SwappedDeath(BrainModuleDeath module, AnimationData stab, AnimationData @default, AnimationData kneel)
            {
                Module = module;
                Stab = stab;
                Default = @default;
                Kneel = kneel;
            }

            public BrainModuleDeath Module { get; }
            public AnimationData Stab { get; }
            public AnimationData Default { get; }
            public AnimationData Kneel { get; }
        }
    }
}
