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
    /// Clash of Might (blade lock), step 5a: the telegraph. An NPC's big swing (long wind-up, read from the running
    /// AttackMotion at AttackStage.WindUp) flashes at the middle of its blade; when the strike starts and the NPC is in
    /// reach, the player's blade near the flash and roughly at a right angle to the swing, time slows down until the
    /// swing ends. The block at the flash point and the clash itself come in the next build steps.
    /// </summary>
    public static class ClashOfMight
    {
        private const string Channel = "Clash";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const float GlintSize = 0.3f;
        private const int GlintTextureSize = 128;

        public static readonly FeatureToggle Toggle = new FeatureToggle(DmcsCategories.ClashOfMight, true, Dmcs.Log);

        private static readonly ClashTelegraph<int> Telegraph = new ClashTelegraph<int>(ClashSettings.JudgeWindow);
        private static readonly Dictionary<int, Flash> Flashes = new Dictionary<int, Flash>();
        private static readonly ClashLock Lock = new ClashLock();

        /// <summary>Whether the blades are locked now, and where (for diagnostics and probes).</summary>
        internal static bool IsLocked => Lock.IsLocked;

        internal static Vector3? LockPoint => Lock.IsLocked && _lockFlash != null && _lockFlash.Weapon != null ? _lockFlash.Point : (Vector3?)null;
        private static int _lockNpc;
        private static Flash _lockFlash;
        private static Item _lockItem;
        private static RagdollHand _lockHand;
        private static float _lockAnimatorSpeed = 1f;
        private static float _lockMass;
        private static bool _hooked;
        private static Material _material;
        private static Texture2D _texture;
        private static bool _shaderMissingLogged;
        private static bool _timeOwned;
        private static float _timeScale;
        private static float _timeEndsAt;
        private const int SoundRate = 48000;
        private static readonly Dictionary<string, System.Threading.Tasks.Task<float[]>> SoundTasks = new Dictionary<string, System.Threading.Tasks.Task<float[]>>();
        private static readonly Dictionary<string, AudioClip> SoundClips = new Dictionary<string, AudioClip>();
        private static GameObject _audioHost;
        private static AudioSource _audio;

        [ModOption(
            name = DmcsCategories.ClashOfMight + " Enabled",
            nameLocalizationId = DmcsCategories.EnabledLocalizationId,
            tooltip = "Blade lock: block an enemy's big swing where its blade flashes, then push back to disarm it.",
            category = DmcsCategories.ClashOfMight,
            categoryOrder = DmcsCategories.ClashOfMightOrder,
            order = 0)]
        public static bool Enabled
        {
            get => Toggle.Enabled;
            set => Toggle.Set(value);
        }

        [ModOption(name = ClashSettings.OnlySwordsName, tooltip = "Only enemies with a sword flash. Off: any blade that cuts.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 1)]
        public static bool OnlySwords { get; set; } = ClashSettings.DefaultOnlySwords;

        [ModOption(name = ClashSettings.MinWindUpName, tooltip = "How long (seconds) an enemy must wind up for its swing to flash. Plain swings take 0.13–0.37 s, big ones 0.40–0.77 s.", valueSourceName = nameof(MinWindUpValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 2)]
        [ModOptionSlider]
        public static float MinWindUp { get; set; } = ClashSettings.MinWindUp.Default;

        [ModOption(name = ClashSettings.SlowMoName, tooltip = "Time slows when a flashing swing comes and your blade is ready to block it.", category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 3)]
        public static bool SlowMo { get; set; } = ClashSettings.DefaultSlowMo;

        [ModOption(name = ClashSettings.SlowMoScaleName, tooltip = "World speed during the clash slow-motion.", valueSourceName = nameof(SlowMoScaleValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 4)]
        [ModOptionSlider]
        public static float SlowMoScale { get; set; } = ClashSettings.SlowMoScale.Default;

        [ModOption(name = ClashSettings.NpcReachName, tooltip = "How close (m) the enemy must be for the slow-motion.", valueSourceName = nameof(NpcReachValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 5)]
        [ModOptionSlider]
        public static float NpcReach { get; set; } = ClashSettings.NpcReach.Default;

        [ModOption(name = ClashSettings.BlockReachName, tooltip = "How close (m) your blade must be to the flash for the slow-motion.", valueSourceName = nameof(BlockReachValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 6)]
        [ModOptionSlider]
        public static float BlockReach { get; set; } = ClashSettings.BlockReach.Default;

        [ModOption(name = ClashSettings.BlockAngleToleranceName, tooltip = "How far (degrees) your blade may be off a right angle to the enemy's swing.", valueSourceName = nameof(BlockAngleToleranceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 7)]
        [ModOptionSlider]
        public static float BlockAngleTolerance { get; set; } = ClashSettings.BlockAngleTolerance.Default;

        [ModOption(name = ClashSettings.PointToleranceName, tooltip = "How close (m) to the flash your block must hit the enemy's blade to lock the blades. Elsewhere it is an ordinary parry.", valueSourceName = nameof(PointToleranceValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 8)]
        [ModOptionSlider]
        public static float PointTolerance { get; set; } = ClashSettings.PointTolerance.Default;

        [ModOption(name = ClashSettings.WeaponWeightName, tooltip = "How much heavier your weapon gets while the blades are locked, so you cannot just push through.", valueSourceName = nameof(WeaponWeightValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 9)]
        [ModOptionSlider]
        public static float WeaponWeight { get; set; } = ClashSettings.WeaponWeight.Default;

        [ModOption(name = ClashSettings.MaxDurationName, tooltip = "Seconds the blades stay locked at most.", valueSourceName = nameof(MaxDurationValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 10)]
        [ModOptionSlider]
        public static float MaxDuration { get; set; } = ClashSettings.MaxDuration.Default;

        public static ModOptionFloat[] PointToleranceValues() => Floats(ClashSettings.PointTolerance, "0.00", " m");

        public static ModOptionFloat[] WeaponWeightValues() => Floats(ClashSettings.WeaponWeight, "0", "x");

        public static ModOptionFloat[] MaxDurationValues() => Floats(ClashSettings.MaxDuration, "0.0", " s");

        [ModOption(name = ClashSettings.FlashSoundName, tooltip = "Sound when an enemy's blade flashes. DMCS 1 is modelled on Sekiro's perilous-attack cue. Combines with the game sound below.", valueSourceName = nameof(FlashSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 11)]
        public static string FlashSoundStyle { get; set; } = ClashSettings.DefaultFlashSound;

        [ModOption(name = ClashSettings.FlashGameSoundName, tooltip = "One of Blade & Sorcery's own sounds with the flash (Metal Hit also sparks). Plays together with the sound above.", valueSourceName = nameof(FlashGameSoundValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 12)]
        public static string FlashGameSound { get; set; } = ClashSettings.DefaultFlashGameSound;

        [ModOption(name = ClashSettings.FlashSoundVolumeName, tooltip = "Volume of the DMCS flash sound.", valueSourceName = nameof(FlashSoundVolumeValues), category = DmcsCategories.ClashOfMight, categoryOrder = DmcsCategories.ClashOfMightOrder, order = 13)]
        [ModOptionSlider]
        public static float FlashSoundVolume { get; set; } = ClashSettings.FlashSoundVolume.Default;

        public static ModOptionString[] FlashSoundValues() => FlashSound.Styles.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionString[] FlashGameSoundValues() => ClashSettings.GameSounds.Select(v => new ModOptionString(v, v)).ToArray();

        public static ModOptionFloat[] FlashSoundVolumeValues() => Floats(ClashSettings.FlashSoundVolume, "0.00", "");

        public static ModOptionFloat[] MinWindUpValues() => Floats(ClashSettings.MinWindUp, "0.00", " s");

        public static ModOptionFloat[] SlowMoScaleValues() => Floats(ClashSettings.SlowMoScale, "0.00", "x");

        public static ModOptionFloat[] NpcReachValues() => Floats(ClashSettings.NpcReach, "0.00", " m");

        public static ModOptionFloat[] BlockReachValues() => Floats(ClashSettings.BlockReach, "0.0", " m");

        public static ModOptionFloat[] BlockAngleToleranceValues() => Floats(ClashSettings.BlockAngleTolerance, "0", "°");

        public static void Start()
        {
            Toggle.Changed -= OnToggle;
            Toggle.Changed += OnToggle;
            if (_hooked)
            {
                return;
            }
            EventManager.onCreatureAttacking += OnCreatureAttacking;
            EventManager.onCreatureAttackParry += OnParry;
            _hooked = true;
        }

        public static void Stop()
        {
            Toggle.Changed -= OnToggle;
            if (_hooked)
            {
                EventManager.onCreatureAttacking -= OnCreatureAttacking;
                EventManager.onCreatureAttackParry -= OnParry;
                _hooked = false;
            }
            ResetAll();
        }

        /// <summary>Level unloading or feature off: flashes gone, our slow-motion ended.</summary>
        public static void ResetAll()
        {
            EndLock(Lock.Break(), "reset");
            foreach (var id in Telegraph.Clear())
            {
                RemoveFlash(id);
            }
            foreach (var id in Flashes.Keys.ToList())
            {
                RemoveFlash(id);
            }
            EndTime(force: true);
        }

        public static void Update()
        {
            UpdateLock();
            if (_timeOwned && Time.unscaledTime >= _timeEndsAt)
            {
                Dmcs.Log.Debug(Channel, "slow-motion cap reached");
                EndTime(force: false);
            }
            if (Flashes.Count == 0)
            {
                return;
            }
            var cam = Player.local != null && Player.local.head != null ? Player.local.head.cam : null;
            foreach (var pair in Flashes.ToList())
            {
                var flash = pair.Value;
                if (flash.Weapon == null || flash.Glint == null || flash.Weapon.mainHandler == null)
                {
                    Telegraph.Ended(pair.Key);
                    RemoveFlash(pair.Key);
                    continue;
                }
                var age = Time.unscaledTime - flash.StartedAt;
                flash.Glint.transform.position = flash.Point;
                flash.Glint.transform.localScale = Vector3.one * (GlintSize * Glint.Scale(age));
                if (cam != null)
                {
                    flash.Glint.transform.rotation = Quaternion.LookRotation(flash.Glint.transform.position - cam.transform.position)
                        * Quaternion.Euler(0f, 0f, Glint.SpinDegrees(age));
                }
                flash.Renderer.material.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f, Glint.Pulse(age)));
            }
        }

        /// <summary>Each physics step: judge the slow-motion of every swing that just started its strike.</summary>
        public static void FixedUpdate()
        {
            if (!Toggle.Enabled || Flashes.Count == 0)
            {
                return;
            }
            var now = Time.unscaledTime;
            foreach (var pair in Flashes.ToList())
            {
                var id = pair.Key;
                if (Telegraph.IsJudging(id, now))
                {
                    var verdict = Judge(pair.Value);
                    if (Telegraph.Verdict(id, verdict, now))
                    {
                        StartTime(pair.Value);
                    }
                }
                var over = Telegraph.JudgingOver(id, now);
                if (over.HasValue)
                {
                    Dmcs.Log.Debug(Channel, "no slow-motion for " + pair.Value.NpcName + "'s swing: " + over.Value);
                }
            }
        }

        private static void OnToggle(bool enabled)
        {
            if (!enabled)
            {
                ResetAll();
            }
        }

        private static void OnCreatureAttacking(Creature attacker, Creature target, Transform targetTransform,
            BrainModuleAttack.AttackType type, BrainModuleAttack.AttackStage stage)
        {
            try
            {
                if (!Toggle.Enabled || attacker == null || attacker.isPlayer || type != BrainModuleAttack.AttackType.Melee)
                {
                    return;
                }
                var id = attacker.GetInstanceID();
                switch (stage)
                {
                    case BrainModuleAttack.AttackStage.WindUp:
                        WindUp(attacker, target, id);
                        break;
                    case BrainModuleAttack.AttackStage.Attack:
                        Telegraph.StrikeStarted(id, Time.unscaledTime);
                        break;
                    case BrainModuleAttack.AttackStage.End:
                    case BrainModuleAttack.AttackStage.Cancel:
                        if (Lock.IsLocked && _lockNpc == id)
                        {
                            break;
                        }
                        if (Telegraph.Ended(id))
                        {
                            EndTime(force: false);
                        }
                        RemoveFlash(id);
                        break;
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash error: " + ex);
            }
        }

        private static void WindUp(Creature attacker, Creature target, int id)
        {
            if (Telegraph.Ended(id))
            {
                EndTime(force: false);
            }
            RemoveFlash(id);
            if (target == null || !target.isPlayer)
            {
                return;
            }
            var melee = attacker.brain.instance?.GetModule<BrainModuleMelee>(false);
            var attack = melee?.attack;
            if (attack == null)
            {
                return;
            }
            var side = attack.attackSide == Interactable.HandSide.Left ? Side.Left : Side.Right;
            var windUp = attack.GetRealStartTime(melee.GetBrainSpeedMultiplier(side));
            var thrust = attack.attackMotion == ItemModuleAI.AttackType.Thrust;
            var weapon = attacker.equipment?.GetHeldItem(side);
            var name = attacker.name;
            if (!ClashRules.IsBigSwing(thrust, windUp, MinWindUp))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " (wind-up " + F(windUp) + " s): not a big swing");
                return;
            }
            if (weapon == null)
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + ": no weapon in the " + side + " hand");
                return;
            }
            if (!TryBladeMiddle(weapon, attacker.GetHand(side), out var local))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " with " + weapon.data?.id + ": no slashing blade ("
                    + string.Join(", ", weapon.collisionHandlers.SelectMany(h => h.damagers).Where(d => d != null)
                        .Select(d => (d.data?.id ?? "?") + "/" + (d.data?.damageModifierData?.damageType.ToString() ?? "?"))) + ")");
                return;
            }
            var isSword = ClashRules.IsSword(weapon.data?.category, weapon.data?.id);
            if (!ClashRules.CanFlash(OnlySwords, isSword, true))
            {
                Dmcs.Log.Debug(Channel, name + " swings " + attack.id + " with " + weapon.data?.id + ": not a sword");
                return;
            }
            var glint = NewGlint();
            if (glint == null)
            {
                return;
            }
            Telegraph.WindUp(id);
            Flashes[id] = new Flash(attacker, weapon, attacker.GetHand(side), local, glint, Time.unscaledTime);
            Dmcs.Log.Debug(Channel, name + " winds up " + attack.id + " (" + F(windUp) + " s) with " + weapon.data?.id + ": flash" + PlayFlashSound(Flashes[id].Point));
        }

        /// <summary>The middle of the weapon's slashing blade along handle → tip, as a point local to the weapon.</summary>
        private static bool TryBladeMiddle(Item weapon, RagdollHand hand, out Vector3 local)
        {
            local = Vector3.zero;
            var handle = hand != null && hand.grabbedHandle != null ? hand.grabbedHandle.transform.position : weapon.transform.position;
            var colliders = new List<Collider>();
            foreach (var handler in weapon.collisionHandlers)
            {
                foreach (var damager in handler.damagers)
                {
                    if (damager == null || damager.data?.damageModifierData == null || damager.data.damageModifierData.damageType != DamageType.Slash)
                    {
                        continue;
                    }
                    if (damager.colliderOnly != null)
                    {
                        colliders.Add(damager.colliderOnly);
                    }
                    else if (damager.colliderGroup != null)
                    {
                        colliders.AddRange(damager.colliderGroup.colliders.Where(c => c != null));
                    }
                }
            }
            if (colliders.Count == 0)
            {
                return false;
            }
            weapon.GetFurthestDamagerCollider(out _, out var tipCollider);
            var tip = tipCollider != null ? tipCollider.bounds.center : colliders[0].bounds.center;
            var axis = (tip - handle).normalized;
            if (axis == Vector3.zero)
            {
                return false;
            }
            var projections = colliders.SelectMany(Corners).Select(p => Vector3.Dot(p - handle, axis));
            var middle = ClashGeometry.MiddleOfExtent(projections);
            if (!middle.HasValue)
            {
                return false;
            }
            local = weapon.transform.InverseTransformPoint(handle + (axis * middle.Value));
            return true;
        }

        private static IEnumerable<Vector3> Corners(Collider c)
        {
            var b = c.bounds;
            for (var i = 0; i < 8; i++)
            {
                yield return b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            }
        }

        private static SlowMoVerdict Judge(Flash flash)
        {
            var player = Player.currentCreature;
            if (player == null)
            {
                return SlowMoVerdict.NpcTooFar;
            }
            var npcDistance = Vector3.Distance(flash.Npc.transform.position, player.transform.position);
            var point = flash.Point;
            var bladeDistance = float.PositiveInfinity;
            Vector3 bladeAxis = Vector3.zero;
            foreach (var hand in new[] { player.handLeft, player.handRight })
            {
                if (!TryPlayerBlade(hand, out var a, out var b))
                {
                    continue;
                }
                var d = ClashGeometry.DistanceToSegment(V(point), V(a), V(b));
                if (d < bladeDistance)
                {
                    bladeDistance = d;
                    bladeAxis = b - a;
                }
            }
            float? angle = null;
            var npcHandle = flash.Hand != null && flash.Hand.grabbedHandle != null ? flash.Hand.grabbedHandle.transform.position : flash.Weapon.transform.position;
            var velocity = flash.Weapon.GetItemPointVelocity(point, true);
            var normal = ClashGeometry.SwingPlaneNormal(V(point - npcHandle), V(velocity), ClashSettings.MinBladeSpeed);
            if (normal.HasValue)
            {
                flash.Normal = new Vector3(normal.Value.X, normal.Value.Y, normal.Value.Z);
            }
            if (normal.HasValue && bladeAxis != Vector3.zero)
            {
                angle = ClashGeometry.AngleToNormal(V(bladeAxis), normal.Value);
            }
            var verdict = ClashRules.JudgeSlowMo(npcDistance, NpcReach, bladeDistance, BlockReach, angle, BlockAngleTolerance);
            flash.LastDetail = "enemy " + F(npcDistance) + " m, blade " + (float.IsInfinity(bladeDistance) ? "none" : F(bladeDistance) + " m")
                + ", angle " + (angle.HasValue ? F(angle.Value) + "°" : "n/a");
            return verdict;
        }

        /// <summary>The player's held blade as handle → tip; false when the hand holds nothing with a damager.</summary>
        private static bool TryPlayerBlade(RagdollHand hand, out Vector3 handle, out Vector3 tip)
        {
            handle = tip = Vector3.zero;
            var item = hand != null && hand.grabbedHandle != null ? hand.grabbedHandle.item : null;
            if (item == null)
            {
                return false;
            }
            item.GetFurthestDamagerCollider(out _, out var tipCollider);
            if (tipCollider == null)
            {
                return false;
            }
            handle = hand.grabbedHandle.transform.position;
            tip = tipCollider.bounds.center;
            return (tip - handle).sqrMagnitude > 0.0001f;
        }

        private static void StartTime(Flash flash)
        {
            if (!SlowMo)
            {
                Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing (" + flash.LastDetail + "); slow-motion is off");
                return;
            }
            if (_timeOwned || TimeManager.slowMotionState != TimeManager.SlowMotionState.Disabled)
            {
                Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing; no slow-motion: another one runs (" + TimeManager.slowMotionState + ")");
                return;
            }
            _timeScale = SlowMoScale;
            TimeManager.SetSlowMotion(true, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.1f, 1f), Catalog.GetData<EffectData>("SpellSlowTime", false));
            _timeOwned = true;
            _timeEndsAt = Time.unscaledTime + ClashSettings.SlowMoMaxSeconds;
            Dmcs.Log.Debug(Channel, "block ready against " + flash.NpcName + "'s swing (" + flash.LastDetail + "): slow-motion " + F(_timeScale) + "x");
        }

        private static void EndTime(bool force)
        {
            if (!_timeOwned)
            {
                return;
            }
            _timeOwned = false;
            var playerDead = Player.currentCreature == null || Player.currentCreature.isKilled;
            // Only end our own slow-motion: the player's Focus, a critical or the game's death slow-motion may have taken over.
            var starting = TimeManager.slowMotionState == TimeManager.SlowMotionState.Starting;
            if (playerDead || (!force && !ClashRules.StillOurSlowMotion(starting, Time.timeScale, _timeScale)))
            {
                Dmcs.Log.Debug(Channel, "slow-motion over; another slow-motion took over, left running");
                return;
            }
            TimeManager.SetSlowMotion(false, _timeScale, AnimationCurve.EaseInOut(0f, 0f, 0.1f, 1f));
            Dmcs.Log.Debug(Channel, "slow-motion over: time back to normal");
        }

        /// <summary>
        /// The NPC's swing hit the player's weapon (the game's own block event, raised by BrainModuleMelee when an attack
        /// hits a held weapon at more than ~1.4 m/s). At the flash point and across the swing the blades lock; anything else
        /// stays an ordinary parry.
        /// </summary>
        private static void OnParry(Creature parried, Item parriedItem, Creature parrying, Item parryingItem, CollisionInstance hit)
        {
            try
            {
                if (!Toggle.Enabled || Lock.IsLocked || parried == null || parried.isPlayer || parrying == null || !parrying.isPlayer || parryingItem == null || hit == null)
                {
                    return;
                }
                var id = parried.GetInstanceID();
                if (!Flashes.TryGetValue(id, out var flash))
                {
                    return;
                }
                if (parriedItem != null && parriedItem != flash.Weapon)
                {
                    Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s other weapon (" + parriedItem.data?.id + "), not the flashing one: an ordinary parry");
                    return;
                }
                var point = flash.Point;
                var contactToFlash = Vector3.Distance(hit.contactPoint, point);
                var hand = parryingItem.mainHandler;
                float? angle = null;
                if (hand != null && TryPlayerBlade(hand, out var a, out var b))
                {
                    var npcHandle = flash.Hand != null && flash.Hand.grabbedHandle != null ? flash.Hand.grabbedHandle.transform.position : flash.Weapon.transform.position;
                    var now = ClashGeometry.SwingPlaneNormal(V(point - npcHandle), V(flash.Weapon.GetItemPointVelocity(point, true)), ClashSettings.MinBladeSpeed);
                    var normal = now ?? (flash.Normal.HasValue ? V(flash.Normal.Value) : (Vec3?)null);
                    if (normal.HasValue)
                    {
                        angle = ClashGeometry.AngleToNormal(V(b - a), normal.Value);
                    }
                }
                var verdict = ClashRules.JudgeBlock(contactToFlash, PointTolerance, angle, BlockAngleTolerance);
                var detail = "contact " + F(contactToFlash) + " m from the flash, angle " + (angle.HasValue ? F(angle.Value) + "°" : "n/a");
                if (verdict != BlockVerdict.Clash)
                {
                    Dmcs.Log.Debug(Channel, "you blocked " + flash.NpcName + "'s swing: " + verdict + " (" + detail + "), an ordinary parry");
                    return;
                }
                StartLock(id, flash, parryingItem, hand, detail);
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash error: " + ex);
            }
        }

        private static void StartLock(int id, Flash flash, Item item, RagdollHand hand, string detail)
        {
            EndTime(force: false);
            _lockNpc = id;
            _lockFlash = flash;
            _lockItem = item;
            _lockHand = hand;
            _lockAnimatorSpeed = flash.Npc.animator != null ? flash.Npc.animator.speed : 1f;
            if (flash.Npc.animator != null)
            {
                flash.Npc.animator.speed = 0f;
            }
            _lockMass = item.physicBody != null ? item.physicBody.mass : 0f;
            if (item.physicBody != null)
            {
                item.physicBody.mass = _lockMass * WeaponWeight;
            }
            Lock.Start(Time.unscaledTime);
            Dmcs.Log.Debug(Channel, "blades locked with " + flash.NpcName + " (" + detail + "): enemy frozen, your " + item.data?.id + " "
                + WeaponWeight.ToString("0", CultureInfo.InvariantCulture) + "x heavier for up to " + F(MaxDuration) + " s");
        }

        private static void UpdateLock()
        {
            if (!Lock.IsLocked)
            {
                return;
            }
            var npc = _lockFlash?.Npc;
            var gone = npc == null || npc.isKilled ? "the enemy is dead or gone"
                : _lockFlash.Weapon == null || _lockFlash.Weapon.mainHandler == null ? "the enemy let go of its weapon"
                : _lockItem == null || _lockItem.mainHandler == null || _lockItem.mainHandler.creature == null || !_lockItem.mainHandler.creature.isPlayer ? "you let go of your weapon"
                : null;
            if (gone != null)
            {
                EndLock(Lock.Break(), gone);
                return;
            }
            var now = Time.unscaledTime;
            if (Lock.DiagnosticDue(now))
            {
                LogDiagnostic(now);
            }
            var end = Lock.Tick(now, MaxDuration);
            if (end != LockEnd.None)
            {
                EndLock(end, "time up (who wins comes in step 5c)");
            }
        }

        /// <summary>
        /// Headset diagnostic for the push measure (step 5c): how far the player's controller (its grip target) is ahead of the
        /// weapon hand that lags behind, overall and towards the enemy; and how far the blades are apart at the flash.
        /// </summary>
        private static void LogDiagnostic(float now)
        {
            var playerHand = _lockHand != null && _lockHand.playerHand != null ? _lockHand.playerHand : null;
            var ahead = playerHand != null && playerHand.grip != null && _lockHand.grip != null ? playerHand.grip.position - _lockHand.grip.position : Vector3.zero;
            var toEnemy = _lockFlash.Npc.transform.position - Player.currentCreature.transform.position;
            toEnemy.y = 0f;
            var towards = toEnemy.sqrMagnitude > 0.0001f ? Vector3.Dot(ahead, toEnemy.normalized) : 0f;
            var apart = TryPlayerBlade(_lockHand, out var a, out var b) ? ClashGeometry.DistanceToSegment(V(_lockFlash.Point), V(a), V(b)) : float.NaN;
            Dmcs.Log.Debug(Channel, "lock t=" + F(Lock.Elapsed(now)) + " s: hand ahead " + F(ahead.magnitude) + " m (towards the enemy " + F(towards)
                + " m), blades " + (float.IsNaN(apart) ? "n/a" : F(apart) + " m") + " apart");
        }

        private static void EndLock(LockEnd end, string why)
        {
            if (end == LockEnd.None)
            {
                return;
            }
            var npc = _lockFlash?.Npc;
            if (npc != null && npc.animator != null)
            {
                npc.animator.speed = _lockAnimatorSpeed;
            }
            if (_lockItem != null && _lockItem.physicBody != null && _lockMass > 0f)
            {
                _lockItem.physicBody.mass = _lockMass;
            }
            Telegraph.Ended(_lockNpc);
            RemoveFlash(_lockNpc);
            Dmcs.Log.Debug(Channel, "blades unlocked (" + end + ": " + why + ")" + (npc != null ? ": " + npc.name + " moves again" : string.Empty));
            _lockFlash = null;
            _lockItem = null;
            _lockHand = null;
        }

        /// <summary>Builds every DMCS flash sound in the background (each takes a moment in the game's Mono), at level load.</summary>
        public static void Prewarm()
        {
            foreach (var style in FlashSound.Sounding)
            {
                Samples(style);
            }
        }

        private static System.Threading.Tasks.Task<float[]> Samples(string style)
        {
            if (!SoundTasks.TryGetValue(style, out var task))
            {
                var spec = FlashSound.Spec(style);
                task = System.Threading.Tasks.Task.Run(() => FlashSound.Generate(SoundRate, 1, spec));
                SoundTasks[style] = task;
            }
            return task;
        }

        /// <summary>The chosen DMCS sound and game sound, together; returns what played, for the log.</summary>
        private static string PlayFlashSound(Vector3 point)
        {
            var played = "";
            try
            {
                var style = FlashSoundStyle;
                if (FlashSound.Spec(style) != null)
                {
                    var task = Samples(style);
                    if (!task.IsCompleted)
                    {
                        played += ", sound " + style + " still building";
                    }
                    else
                    {
                        if (!SoundClips.TryGetValue(style, out var clip) || clip == null)
                        {
                            var samples = task.Result;
                            clip = AudioClip.Create("DMCS Clash Flash (" + style + ")", samples.Length / 2, 2, SoundRate, false);
                            clip.SetData(samples, 0);
                            SoundClips[style] = clip;
                        }
                        EnsureAudio();
                        _audio.PlayOneShot(clip, FlashSoundVolume);
                        played += ", sound " + style + " (" + clip.length.ToString("0.00", CultureInfo.InvariantCulture) + " s)";
                    }
                }
                var effectId = ClashSettings.GameSoundEffect(FlashGameSound);
                if (effectId != null)
                {
                    var effect = Catalog.GetData<EffectData>(effectId, false)?.Spawn(point, Quaternion.identity);
                    if (effect != null)
                    {
                        effect.Play();
                        played += ", game sound " + FlashGameSound;
                    }
                    else
                    {
                        played += ", game sound " + FlashGameSound + " missing (" + effectId + ")";
                    }
                }
            }
            catch (Exception ex)
            {
                Dmcs.Log.Info("Clash flash sound error: " + ex);
            }
            return played;
        }

        private static void EnsureAudio()
        {
            if (_audio != null)
            {
                return;
            }
            _audioHost = new GameObject("DMCS Clash Audio");
            UnityEngine.Object.DontDestroyOnLoad(_audioHost);
            _audio = _audioHost.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
        }

        private static GameObject NewGlint()
        {
            if (_material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    if (!_shaderMissingLogged)
                    {
                        Dmcs.Log.Info("Clash flash: shader '" + ShaderName + "' not found; no flash.");
                        _shaderMissingLogged = true;
                    }
                    return null;
                }
                _texture = new Texture2D(GlintTextureSize, GlintTextureSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    name = "DMCS Clash Glint",
                };
                _texture.LoadRawTextureData(Glint.Pixels(GlintTextureSize));
                _texture.Apply(false, true);
                _material = new Material(shader) { name = "DMCS Clash Glint" };
                _material.SetFloat("_Surface", 1f);
                _material.SetFloat("_Blend", 2f);
                _material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _material.SetFloat("_ZWrite", 0f);
                _material.SetFloat("_Cull", 0f);
                _material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _material.SetTexture("_BaseMap", _texture);
                _material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "DMCS Clash Flash";
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * GlintSize;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.material.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f, 0f));
            return go;
        }

        private static void RemoveFlash(int id)
        {
            if (Flashes.TryGetValue(id, out var flash))
            {
                if (flash.Glint != null)
                {
                    UnityEngine.Object.Destroy(flash.Renderer.material);
                    UnityEngine.Object.Destroy(flash.Glint);
                }
                Flashes.Remove(id);
            }
        }

        private static Vec3 V(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();

        private sealed class Flash
        {
            public Flash(Creature npc, Item weapon, RagdollHand hand, Vector3 local, GameObject glint, float startedAt)
            {
                Npc = npc;
                NpcName = npc.name;
                Weapon = weapon;
                Hand = hand;
                Local = local;
                Glint = glint;
                Renderer = glint.GetComponent<MeshRenderer>();
                StartedAt = startedAt;
            }

            public Creature Npc { get; }
            public string NpcName { get; }
            public Item Weapon { get; }
            public RagdollHand Hand { get; }
            public Vector3 Local { get; }
            public GameObject Glint { get; }
            public MeshRenderer Renderer { get; }
            public float StartedAt { get; }
            public string LastDetail { get; set; } = "";

            /// <summary>The swing plane's normal from the strike start (null until read).</summary>
            public Vector3? Normal { get; set; }

            public Vector3 Point => Weapon.transform.TransformPoint(Local);
        }
    }
}
