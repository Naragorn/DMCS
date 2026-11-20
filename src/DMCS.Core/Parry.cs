using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>Parry's Mod Menu names, ranges and defaults (spec section 6: Counter Parry and Power Parry, built 2026-11-20).</summary>
    public static class ParrySettings
    {
        public const string EnabledName = DmcsCategories.Parry + " Enabled";
        public const string CounterName = "Counter Parry";
        public const string CounterSpeedName = "Counter Guard Speed";
        public const string StaggerDurationName = "Counter Stagger Duration";
        public const string PowerName = "Power Parry";
        public const string PowerSpeedName = "Power Parry Min Speed";
        public const string PowerChanceName = "Power Parry Chance";
        public const string SpeedWindowName = "Parry Speed Window";

        public const bool DefaultEnabled = true;
        public const bool DefaultCounter = true;

        /// <summary>User 2026-11-20: no stagger gate (an attacking enemy is never staggered), chance 100 %.</summary>
        public const bool DefaultPower = true;

        /// <summary>
        /// m/s the player's controller must reach just before the parry for a counter: the guard came up in time instead of
        /// being held there. A guess for the headset.
        /// </summary>
        public static readonly SliderSpec CounterSpeed = new SliderSpec(CounterSpeedName, 0.25f, 4f, 0.25f, 1f);

        /// <summary>m/s of the controller for a power parry (a hard swing into the attack). A guess for the headset.</summary>
        public static readonly SliderSpec PowerSpeed = new SliderSpec(PowerSpeedName, 1f, 10f, 0.25f, 3f);

        public static readonly SliderSpec PowerChance = new SliderSpec(PowerChanceName, 0f, 100f, 5f, 100f);

        /// <summary>Seconds DMCS counts a countered enemy as staggered (Disarm Strike's hand hit and Twist Disarm need it).</summary>
        public static readonly SliderSpec StaggerDuration = new SliderSpec(StaggerDurationName, 0.5f, 5f, 0.25f, 1.5f);

        /// <summary>Seconds before the parry in which the controller's fastest speed counts.</summary>
        public static readonly SliderSpec SpeedWindow = new SliderSpec(SpeedWindowName, 0.05f, 0.5f, 0.05f, 0.2f);

        public static IEnumerable<SliderSpec> Sliders => new[] { CounterSpeed, StaggerDuration, PowerSpeed, PowerChance, SpeedWindow };
    }

    public enum ParryDecision
    {
        Disabled,
        NotPlayerParry,
        TakenByClash,
        PowerParry,
        KeepsShield,
        NoWeapon,
        PowerChanceFailed,
        Counter,
        TooSlow,
    }

    /// <summary>One player parry of an enemy attack (plain values read from the game's parry event by the caller).</summary>
    public readonly struct ParryContact
    {
        public ParryContact(
            bool enabled, bool counterOn, bool powerOn, bool playerParry, bool clashTookIt, bool enemyHoldsWeapon,
            bool keepsShield, float handSpeed, float counterMinSpeed, float powerMinSpeed, float roll, float chance)
        {
            Enabled = enabled;
            CounterOn = counterOn;
            PowerOn = powerOn;
            PlayerParry = playerParry;
            ClashTookIt = clashTookIt;
            EnemyHoldsWeapon = enemyHoldsWeapon;
            KeepsShield = keepsShield;
            HandSpeed = handSpeed;
            CounterMinSpeed = counterMinSpeed;
            PowerMinSpeed = powerMinSpeed;
            Roll = roll;
            Chance = chance;
        }

        public bool Enabled { get; }
        public bool CounterOn { get; }
        public bool PowerOn { get; }

        /// <summary>A living NPC's attack, parried by the player's held weapon.</summary>
        public bool PlayerParry { get; }

        /// <summary>Clash of Might locked the blades or knocked the sword away (Simple) on this very parry.</summary>
        public bool ClashTookIt { get; }

        /// <summary>The NPC still holds the weapon whose attack was parried.</summary>
        public bool EnemyHoldsWeapon { get; }

        /// <summary>That weapon is a shield and shields cannot be disarmed (Disarm Strike's setting).</summary>
        public bool KeepsShield { get; }

        /// <summary>The fastest speed (m/s) of the parrying hand's controller just before the parry.</summary>
        public float HandSpeed { get; }

        public float CounterMinSpeed { get; }
        public float PowerMinSpeed { get; }

        /// <summary>0..1 dice.</summary>
        public float Roll { get; }

        /// <summary>0..1.</summary>
        public float Chance { get; }
    }

    public readonly struct ParryVerdict
    {
        public ParryVerdict(ParryDecision decision, bool staggers)
        {
            Decision = decision;
            Staggers = staggers;
        }

        public ParryDecision Decision { get; }

        /// <summary>The enemy is staggered (a counter; a power parry counts as one too while Counter Parry is on).</summary>
        public bool Staggers { get; }

        public bool Disarms => Decision == ParryDecision.PowerParry;
    }

    /// <summary>
    /// Counter Parry and Power Parry (spec section 6). One measure decides both: how fast the player's real controller
    /// moved just before the parry. A guard held in place is an ordinary parry (the game's own recoil); a guard brought up
    /// in time (at least the counter speed) staggers the enemy, as Dark Messiah's counter; a hard swing into the attack (at
    /// least the power speed) also knocks its weapon away, with no stagger gate (user 2026-11-20: an attacking enemy is
    /// never staggered). The controller, not the weapon: at the event the collision has already slowed the blade, and the
    /// enemy's swing is part of the impact speed. A power parry is never a clash; below it, Clash of Might decides first.
    /// </summary>
    public static class ParryRule
    {
        public static ParryVerdict Decide(ParryContact c)
        {
            if (!c.Enabled || (!c.CounterOn && !c.PowerOn))
            {
                return new ParryVerdict(ParryDecision.Disabled, false);
            }
            if (!c.PlayerParry)
            {
                return new ParryVerdict(ParryDecision.NotPlayerParry, false);
            }
            if (c.ClashTookIt)
            {
                return new ParryVerdict(ParryDecision.TakenByClash, false);
            }
            var staggers = c.CounterOn && c.HandSpeed >= c.CounterMinSpeed;
            if (c.PowerOn && c.HandSpeed >= c.PowerMinSpeed)
            {
                if (c.KeepsShield)
                {
                    return new ParryVerdict(ParryDecision.KeepsShield, staggers);
                }
                if (!c.EnemyHoldsWeapon)
                {
                    return new ParryVerdict(ParryDecision.NoWeapon, staggers);
                }
                return new ParryVerdict(c.Roll < c.Chance ? ParryDecision.PowerParry : ParryDecision.PowerChanceFailed, staggers);
            }
            return staggers ? new ParryVerdict(ParryDecision.Counter, true) : new ParryVerdict(ParryDecision.TooSlow, false);
        }

        /// <summary>A parry hard enough for a power parry: Clash of Might leaves it alone (spec section 6).</summary>
        public static bool TakesOverFromClash(bool enabled, bool powerOn, float handSpeed, float powerMinSpeed) =>
            enabled && powerOn && handSpeed >= powerMinSpeed;

        /// <summary>Decisions not worth a log line (feature off, or not the player parrying an enemy).</summary>
        public static bool IsQuiet(ParryDecision d) => d == ParryDecision.Disabled || d == ParryDecision.NotPlayerParry;
    }
}
