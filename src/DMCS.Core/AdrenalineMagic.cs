using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    /// <summary>A spell element the adrenaline magic knows (the game's catalog spell ids).</summary>
    public enum MagicElement
    {
        None,
        Fire,
        Lightning,
        Gravity,
    }

    public enum MagicDecision
    {
        /// <summary>The hit or push did not come from a player spell.</summary>
        NotASpell,
        AdrenalineOff,
        MagicOff,

        /// <summary>A player spell DMCS has no big version for.</summary>
        UnknownElement,
        ElementOff,
        TargetIsPlayer,
        TargetDead,

        /// <summary>The adrenaline window is not open: an ordinary spell hit.</summary>
        NotReady,

        /// <summary>The magic critical.</summary>
        Magic,
    }

    /// <summary>The facts and settings for one player spell hit or push (plain values, read by the caller).</summary>
    public readonly struct MagicCast
    {
        public MagicCast(bool adrenalineOn, bool magicOn, bool ready, bool bySpell, MagicElement element, bool elementOn, bool targetIsPlayer, bool targetDead)
        {
            AdrenalineOn = adrenalineOn;
            MagicOn = magicOn;
            Ready = ready;
            BySpell = bySpell;
            Element = element;
            ElementOn = elementOn;
            TargetIsPlayer = targetIsPlayer;
            TargetDead = targetDead;
        }

        public bool AdrenalineOn { get; }
        public bool MagicOn { get; }

        /// <summary>The meter is full (window open).</summary>
        public bool Ready { get; }

        public bool BySpell { get; }
        public MagicElement Element { get; }
        public bool ElementOn { get; }
        public bool TargetIsPlayer { get; }

        /// <summary>Dead before this hit.</summary>
        public bool TargetDead { get; }
    }

    /// <summary>Another creature near the struck one: who the area part of the magic reaches.</summary>
    public readonly struct MagicTarget
    {
        public MagicTarget(int id, float distance, bool alive, bool isPlayer)
        {
            Id = id;
            Distance = distance;
            Alive = alive;
            IsPlayer = isPlayer;
        }

        public int Id { get; }
        public float Distance { get; }
        public bool Alive { get; }
        public bool IsPlayer { get; }
    }

    /// <summary>
    /// Adrenaline magic (user 2026-11-20): while the adrenaline window is open, the next player spell that hits an enemy is
    /// the critical, with a big version per element instead of the melee fatality. Fire: the struck enemy burns up, those
    /// around catch fire. Lightning: the bolt jumps to every enemy around, all are shocked, the struck one dies. Gravity: a
    /// push flings every enemy around away, the nearest one it hit dies.
    /// </summary>
    public static class MagicRule
    {
        public static MagicElement ElementOf(string spellId)
        {
            switch (spellId)
            {
                case "Fire": return MagicElement.Fire;
                case "Lightning": return MagicElement.Lightning;
                case "Gravity": return MagicElement.Gravity;
                default: return MagicElement.None;
            }
        }

        public static MagicDecision Decide(MagicCast c)
        {
            if (!c.BySpell)
            {
                return MagicDecision.NotASpell;
            }
            if (!c.AdrenalineOn)
            {
                return MagicDecision.AdrenalineOff;
            }
            if (!c.MagicOn)
            {
                return MagicDecision.MagicOff;
            }
            if (c.Element == MagicElement.None)
            {
                return MagicDecision.UnknownElement;
            }
            if (!c.ElementOn)
            {
                return MagicDecision.ElementOff;
            }
            if (c.TargetIsPlayer)
            {
                return MagicDecision.TargetIsPlayer;
            }
            if (c.TargetDead)
            {
                return MagicDecision.TargetDead;
            }
            return c.Ready ? MagicDecision.Magic : MagicDecision.NotReady;
        }

        public static bool IsMagic(MagicDecision decision) => decision == MagicDecision.Magic;

        /// <summary>The living enemies within <paramref name="radius"/> m, nearest first.</summary>
        public static List<int> Around(IEnumerable<MagicTarget> others, float radius)
        {
            if (others == null || radius <= 0f)
            {
                return new List<int>();
            }
            return others.Where(o => o.Alive && !o.IsPlayer && o.Distance <= radius).OrderBy(o => o.Distance).Select(o => o.Id).ToList();
        }
    }

    /// <summary>Adrenaline Magic's Mod Menu names, ranges and defaults (all guesses for the headset).</summary>
    public static class AdrenalineMagicSettings
    {
        public const string EnabledName = DmcsCategories.AdrenalineMagic + " Enabled";
        public const string FireName = "Magic Fire";
        public const string LightningName = "Magic Lightning";
        public const string GravityName = "Magic Gravity";

        public const bool DefaultEnabled = true;
        public const bool DefaultFire = true;
        public const bool DefaultLightning = true;
        public const bool DefaultGravity = true;

        /// <summary>The game's full heat (Burning.maxHeat): at 100 the creature ignites and dies the fire death.</summary>
        public const float FullHeat = 100f;

        public static readonly SliderSpec FireRadius = new SliderSpec("Magic Fire Radius", 0f, 10f, 0.5f, 3f);

        /// <summary>Heat for those around (the game's own fireball adds 40 with the Ignition skill).</summary>
        public static readonly SliderSpec FireSpreadHeat = new SliderSpec("Magic Fire Spread Heat", 0f, 100f, 5f, 50f);

        public static readonly SliderSpec LightningRadius = new SliderSpec("Magic Lightning Radius", 0f, 15f, 0.5f, 5f);

        /// <summary>Seconds of Electrocute (the game's own bolt: 2 s, 4 s on metal armour).</summary>
        public static readonly SliderSpec LightningShock = new SliderSpec("Magic Lightning Shock", 0.5f, 10f, 0.5f, 4f);

        public static readonly SliderSpec GravityRadius = new SliderSpec("Magic Gravity Radius", 0f, 15f, 0.5f, 4f);

        /// <summary>m/s every part of a flung enemy gets, away from the player.</summary>
        public static readonly SliderSpec GravityForce = new SliderSpec("Magic Gravity Force", 2f, 30f, 1f, 12f);

        public static IEnumerable<SliderSpec> Sliders => new[] { FireRadius, FireSpreadHeat, LightningRadius, LightningShock, GravityRadius, GravityForce };
    }
}
