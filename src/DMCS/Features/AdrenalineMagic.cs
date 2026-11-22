using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DMCS.Core;
using ThunderRoad;
using ThunderRoad.Skill.Spell;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// Adrenaline magic (user 2026-11-20, rule MagicRule): while the adrenaline window is open, the next player spell that
    /// hits an enemy is the critical, with a big version per element. Read in the game code 1.3.2:
    /// - Fire: the fireball is an item with ItemMagicProjectile; its hit raises onCreatureHit through Creature.Damage, with
    ///   the projectile as source; imbueSpellCastCharge is the casting spell ("Fire"). The game's own burn on a fireball hit
    ///   (skill Ignition, SkillBurning) inflicts "Burning" with a heat parameter (a boxed float); at 100 heat the creature
    ///   ignites and dies the fire death (Burning.Ignite).
    /// - Lightning: SpellCastLightning.Hit damages with CollisionInstance.casterHand = the casting hand and DamageType
    ///   Lightning (merges and skills use Energy); "Electrocute" takes no parameter.
    /// - Gravity: the push (SpellCastGravity.Throw) damages nothing, so no hit event: DMCS listens to the player's gravity
    ///   spell's OnPostPushEvent (the creatures it pushed, the caster included), found through Mana.OnSpellLoadEvent.
    /// The magic replaces the melee critical for that hit: battle cry and time mode as the critical, no cut, its own kill.
    /// </summary>
    public static class AdrenalineMagic
    {
        private const string Channel = "Adrenaline Magic";
        private const string BurningId = "Burning";
        private const string ElectrocuteId = "Electrocute";

        /// <summary>The handler DMCS inflicts its statuses under (the game keys a status's sources by handler).</summary>
        private static readonly object Handler = new object();

        private static readonly Dictionary<CollisionInstance, MagicElement> Pending = new Dictionary<CollisionInstance, MagicElement>();
        private static readonly HashSet<SpellCastGravity> Pushes = new HashSet<SpellCastGravity>();
        private static Mana _mana;
        private static bool _hooked;

        [ModOption(name = AdrenalineMagicSettings.EnabledName, nameLocalizationId = DmcsCategories.EnabledLocalizationId, tooltip = "With a full adrenaline meter, your next spell that hits is the critical: a big version of Fire, Lightning or Gravity. Needs Adrenaline on.", category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 0)]
        public static bool Enabled { get; set; } = AdrenalineMagicSettings.DefaultEnabled;

        [ModOption(name = AdrenalineMagicSettings.FireName, tooltip = "A fireball burns the enemy up at once; enemies around it catch fire.", category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 1)]
        public static bool FireEnabled { get; set; } = AdrenalineMagicSettings.DefaultFire;

        [ModOption(name = "Magic Fire Radius", tooltip = "How far (m) around the burning enemy others catch fire. 0 = only the one hit.", valueSourceName = nameof(FireRadiusValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 2)]
        [ModOptionSlider]
        public static float FireRadius { get; set; } = AdrenalineMagicSettings.FireRadius.Default;

        [ModOption(name = "Magic Fire Spread Heat", tooltip = "How much heat the enemies around get (the game's burn: 100 = they burn up too).", valueSourceName = nameof(FireSpreadHeatValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 3)]
        [ModOptionSlider]
        public static float FireSpreadHeat { get; set; } = AdrenalineMagicSettings.FireSpreadHeat.Default;

        [ModOption(name = AdrenalineMagicSettings.LightningName, tooltip = "A lightning bolt jumps to every enemy around and shocks them all; the one hit dies.", category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 4)]
        public static bool LightningEnabled { get; set; } = AdrenalineMagicSettings.DefaultLightning;

        [ModOption(name = "Magic Lightning Radius", tooltip = "How far (m) the bolt jumps from the enemy hit.", valueSourceName = nameof(LightningRadiusValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 5)]
        [ModOptionSlider]
        public static float LightningRadius { get; set; } = AdrenalineMagicSettings.LightningRadius.Default;

        [ModOption(name = "Magic Lightning Shock", tooltip = "How long (s) the enemies around are shocked.", valueSourceName = nameof(LightningShockValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 6)]
        [ModOptionSlider]
        public static float LightningShock { get; set; } = AdrenalineMagicSettings.LightningShock.Default;

        [ModOption(name = AdrenalineMagicSettings.GravityName, tooltip = "A gravity push flings every enemy around you away; the nearest one it hit dies.", category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 7)]
        public static bool GravityEnabled { get; set; } = AdrenalineMagicSettings.DefaultGravity;

        [ModOption(name = "Magic Gravity Radius", tooltip = "How far (m) around you enemies are flung.", valueSourceName = nameof(GravityRadiusValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 8)]
        [ModOptionSlider]
        public static float GravityRadius { get; set; } = AdrenalineMagicSettings.GravityRadius.Default;

        [ModOption(name = "Magic Gravity Force", tooltip = "How hard (m/s) the enemies are flung away from you.", valueSourceName = nameof(GravityForceValues), category = DmcsCategories.AdrenalineMagic, categoryOrder = DmcsCategories.AdrenalineMagicOrder, order = 9)]
        [ModOptionSlider]
        public static float GravityForce { get; set; } = AdrenalineMagicSettings.GravityForce.Default;

        public static ModOptionFloat[] FireRadiusValues() => Floats(AdrenalineMagicSettings.FireRadius, "0.0", " m");

        public static ModOptionFloat[] FireSpreadHeatValues() => Floats(AdrenalineMagicSettings.FireSpreadHeat, "0", "");

        public static ModOptionFloat[] LightningRadiusValues() => Floats(AdrenalineMagicSettings.LightningRadius, "0.0", " m");

        public static ModOptionFloat[] LightningShockValues() => Floats(AdrenalineMagicSettings.LightningShock, "0.0", " s");

        public static ModOptionFloat[] GravityRadiusValues() => Floats(AdrenalineMagicSettings.GravityRadius, "0.0", " m");

        public static ModOptionFloat[] GravityForceValues() => Floats(AdrenalineMagicSettings.GravityForce, "0", " m/s");

        public static void Start()
        {
            if (_hooked)
            {
                return;
            }
            EventManager.onPossess += OnPossess;
            _hooked = true;
            Watch(Player.currentCreature);
        }

        public static void Stop()
        {
            if (!_hooked)
            {
                return;
            }
            EventManager.onPossess -= OnPossess;
            Watch(null);
            _hooked = false;
            Pending.Clear();
        }

        public static void Update() => Pending.Clear();

        /// <summary>At the hit's start: whether this player spell hit is the magic critical (then the melee critical is skipped).</summary>
        public static bool Claim(Creature target, CollisionInstance hit, HitSource source, bool adrenalineOn, bool ready)
        {
            var element = source == HitSource.PlayerSpell ? ElementOf(hit) : MagicElement.None;
            var decision = MagicRule.Decide(new MagicCast(
                adrenalineOn, Enabled, ready, source == HitSource.PlayerSpell, element, ElementOn(element), target.isPlayer, target.isKilled));
            if (decision == MagicDecision.Magic)
            {
                Pending[hit] = element;
                return true;
            }
            if (ready && decision != MagicDecision.NotASpell)
            {
                Dmcs.Log.Debug(Channel, "no magic on " + target.name + ": " + decision + " (" + element + ")");
            }
            return false;
        }

        /// <summary>At the hit's end: the element of a claimed hit (and forgets it).</summary>
        public static bool TakePending(CollisionInstance hit, out MagicElement element) => Pending.TryGetValue(hit, out element) && Pending.Remove(hit);

        /// <summary>The magic critical of a spell hit (the meter is already used up).</summary>
        public static void Execute(MagicElement element, Creature target, CollisionInstance hit)
        {
            AdrenalineCritical.Moment("magic critical: " + element + " on " + target.name);
            switch (element)
            {
                case MagicElement.Fire:
                    Burn(target, hit);
                    break;
                case MagicElement.Lightning:
                    Shock(target, hit);
                    break;
            }
        }

        /// <summary>The spell of a player spell hit: the fireball's own spell, or the casting hand's (the bolt only).</summary>
        private static MagicElement ElementOf(CollisionInstance hit)
        {
            var item = hit.sourceColliderGroup?.collisionHandler?.item;
            var projectile = item != null ? item.GetComponent<ItemMagicProjectile>() : null;
            if (projectile != null && projectile.imbueSpellCastCharge != null)
            {
                return MagicRule.ElementOf(projectile.imbueSpellCastCharge.id);
            }
            var element = MagicRule.ElementOf(hit.casterHand?.spellInstance?.id);
            // Lightning merges and skills hit with Energy; only the bolt itself is Lightning.
            return element == MagicElement.Lightning && hit.damageStruct.damageType != DamageType.Lightning ? MagicElement.None : element;
        }

        private static bool ElementOn(MagicElement element)
        {
            switch (element)
            {
                case MagicElement.Fire: return FireEnabled;
                case MagicElement.Lightning: return LightningEnabled;
                case MagicElement.Gravity: return GravityEnabled;
                default: return false;
            }
        }

        private static void Burn(Creature target, CollisionInstance hit)
        {
            target.Inflict(BurningId, Handler, float.PositiveInfinity, AdrenalineMagicSettings.FullHeat);
            var around = Around(target, target.ragdoll.targetPart.transform.position, FireRadius);
            if (FireSpreadHeat > 0f)
            {
                foreach (var c in around)
                {
                    c.Inflict(BurningId, Handler, float.PositiveInfinity, FireSpreadHeat);
                }
            }
            var burns = target.HasStatus(BurningId);
            if (!burns && AdrenalineCritical.Kills && !target.isKilled)
            {
                // The game refuses the burn in water: the critical still kills.
                target.Kill(hit);
            }
            Dmcs.Log.Debug(Channel, target.name + (burns ? " burns up (heat " + F(AdrenalineMagicSettings.FullHeat) + ")" : " does not burn (in water?)")
                + "; " + Names(around) + (around.Count > 0 && FireSpreadHeat > 0f ? " catch fire (heat " + F(FireSpreadHeat) + ")" : " around"));
        }

        private static void Shock(Creature target, CollisionInstance hit)
        {
            var bolt = hit.casterHand?.spellInstance as SpellCastLightning;
            var from = target.ragdoll.targetPart.transform.position;
            var around = Around(target, from, LightningRadius);
            target.Inflict(ElectrocuteId, Handler, LightningShock);
            foreach (var c in around)
            {
                bolt?.PlayBolt(from, c.ragdoll.targetPart.transform.position);
                c.Inflict(ElectrocuteId, Handler, LightningShock);
            }
            if (AdrenalineCritical.Kills && !target.isKilled)
            {
                target.Kill(hit);
            }
            Dmcs.Log.Debug(Channel, "the bolt jumps to " + Names(around) + ", shocked for " + F(LightningShock) + " s"
                + (target.isKilled ? "; " + target.name + " dies" : ""));
        }

        private static void Fling(Creature nearest, IReadOnlyCollection<Creature> pushed)
        {
            var player = Player.currentCreature;
            var center = player != null ? player.transform.position : nearest.transform.position;
            var around = Around(null, center, GravityRadius);
            foreach (var c in pushed.Where(c => !around.Contains(c)))
            {
                around.Add(c);
            }
            if (AdrenalineCritical.Kills && !nearest.isKilled)
            {
                nearest.Kill();
            }
            foreach (var c in around)
            {
                var away = c.transform.position - center;
                away.y = 0f;
                var direction = (away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward) + (Vector3.up * 0.3f);
                AdrenalineCritical.Fling(c, direction.normalized, GravityForce);
            }
            Dmcs.Log.Debug(Channel, "flung " + Names(around) + " at " + F(GravityForce) + " m/s"
                + (nearest.isKilled ? "; " + nearest.name + " dies" : ""));
        }

        /// <summary>Living non-player creatures within the radius of a point, nearest first, without the given one.</summary>
        private static List<Creature> Around(Creature except, Vector3 point, float radius)
        {
            var all = Creature.allActive.Where(c => c != null && c != except && c.ragdoll != null).ToList();
            var ids = MagicRule.Around(all.Select(c => new MagicTarget(
                c.GetInstanceID(), Vector3.Distance(point, c.ragdoll.targetPart.transform.position), !c.isKilled, c.isPlayer)), radius);
            return ids.Select(id => all.First(c => c.GetInstanceID() == id)).ToList();
        }

        private static string Names(IReadOnlyCollection<Creature> creatures) =>
            creatures.Count == 0 ? "nobody" : string.Join(", ", creatures.Select(c => c.name));

        private static void OnPossess(Creature creature, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd && creature != null && creature.isPlayer)
            {
                Watch(creature);
            }
        }

        /// <summary>Follows the player's spells, to hear its gravity pushes.</summary>
        private static void Watch(Creature player)
        {
            if (_mana != null)
            {
                _mana.OnSpellLoadEvent -= OnSpellLoad;
                _mana.OnSpellUnloadEvent -= OnSpellUnload;
            }
            foreach (var push in Pushes)
            {
                push.OnPostPushEvent -= OnPostPush;
            }
            Pushes.Clear();
            _mana = player?.mana;
            if (_mana == null)
            {
                return;
            }
            _mana.OnSpellLoadEvent += OnSpellLoad;
            _mana.OnSpellUnloadEvent += OnSpellUnload;
            foreach (var hand in new[] { player.handLeft, player.handRight })
            {
                OnSpellLoad(hand?.caster?.spellInstance, hand?.caster);
            }
        }

        private static void OnSpellLoad(SpellData spell, SpellCaster caster)
        {
            if (spell is SpellCastGravity gravity && Pushes.Add(gravity))
            {
                gravity.OnPostPushEvent += OnPostPush;
                Dmcs.Log.Debug(Channel, "listening to the gravity spell on the " + (caster?.ragdollHand != null ? caster.ragdollHand.side.ToString() : "?") + " hand");
            }
        }

        private static void OnSpellUnload(SpellData spell, SpellCaster caster)
        {
            if (spell is SpellCastGravity gravity && Pushes.Remove(gravity))
            {
                gravity.OnPostPushEvent -= OnPostPush;
            }
        }

        private static void OnPostPush(SpellCastGravity spell, Vector3 pushVector, HashSet<Item> items, HashSet<Creature> creatures)
        {
            try
            {
                var player = spell?.spellCaster?.mana?.creature;
                if (player == null || !player.isPlayer || creatures == null)
                {
                    return;
                }
                var pushed = creatures.Where(c => c != null && !c.isPlayer && !c.isKilled).ToList();
                if (pushed.Count == 0)
                {
                    if (Adrenaline.IsReady)
                    {
                        Dmcs.Log.Debug(Channel, "the gravity push hit no enemy");
                    }
                    return;
                }
                var nearest = pushed.OrderBy(c => (c.transform.position - player.transform.position).sqrMagnitude).First();
                var ready = Adrenaline.IsReady;
                var decision = MagicRule.Decide(new MagicCast(
                    Adrenaline.Toggle.Enabled, Enabled, ready, true, MagicElement.Gravity, GravityEnabled, false, false));
                if (decision != MagicDecision.Magic)
                {
                    if (ready)
                    {
                        Dmcs.Log.Debug(Channel, "no magic on " + nearest.name + ": " + decision + " (Gravity)");
                    }
                    return;
                }
                if (!Adrenaline.UseForMagic(nearest))
                {
                    return;
                }
                AdrenalineCritical.Moment("magic critical: Gravity on " + nearest.name);
                Fling(nearest, pushed);
            }
            catch (Exception e)
            {
                Dmcs.Log.Info("Adrenaline Magic error: " + e);
            }
        }

        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private static ModOptionFloat[] Floats(SliderSpec spec, string format, string unit) =>
            spec.Values().Select(v => new ModOptionFloat(v.ToString(format, CultureInfo.InvariantCulture) + unit, v)).ToArray();
    }
}
