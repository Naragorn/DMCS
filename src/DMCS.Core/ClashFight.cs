using System;
using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>Clash of Might's feature mode (user 2026-11-18): Off, the full lock, or the simple variant.</summary>
    public static class ClashModes
    {
        public const string Off = "Off";
        public const string On = "On";

        /// <summary>The star comes; a block exactly at the star knocks the enemy's sword away. No lock, no pushing.</summary>
        public const string Simple = "Simple";

        public const string ModeName = "Clash of Might Mode";
        public const string Default = Simple;

        public static readonly string[] All = { Off, Simple, On };

        public static string Normalize(string mode) => Array.IndexOf(All, mode) >= 0 ? mode : Default;

        public static bool IsActive(string mode) => Normalize(mode) != Off;

        public static bool Locks(string mode) => Normalize(mode) == On;
    }

    /// <summary>What happens to the player after a lost clash (spec: `clashLoseOutcome`).</summary>
    public static class ClashLoseOutcomes
    {
        public const string DisarmAndHit = "Disarm + Hit";
        public const string Disarm = "Disarm";
        public const string Hit = "Hit";
        public const string None = "None";

        public static readonly string[] All = { DisarmAndHit, Disarm, Hit, None };

        public static bool Disarms(string outcome) => outcome == DisarmAndHit || outcome == Disarm;

        public static bool Hits(string outcome) => outcome == DisarmAndHit || outcome == Hit;
    }

    /// <summary>Settings of the lock rounds (step 5c, user 2026-11-18). Every value but the female strength is a guess for the headset.</summary>
    public static class ClashFightSettings
    {
        public const string DifficultyName = "Clash Difficulty";
        public const string ScalesWithHeightName = "Clash Difficulty Scales With Height";
        public const string ScalesWithGenderName = "Clash Difficulty Scales With Gender";
        public const string FemaleStrengthName = "Clash Female Strength Reduction";
        public const string MinRoundsName = "Clash Min Rounds";
        public const string MaxRoundsName = "Clash Max Rounds";
        public const string FewerRoundsName = "Clash Fewer Rounds Against Weaker";
        public const string EnemyPushName = "Clash Enemy Push";
        public const string DeadZoneName = "Clash Push Dead Zone";
        public const string PushPhaseName = "Clash Push Phase";
        public const string ReturnWindowName = "Clash Return Window";
        public const string ReturnToleranceName = "Clash Return Tolerance";
        public const string WinDisarmsName = "Clash Win Disarms";
        public const string WinKneelChanceName = "Clash Win Kneel Chance";
        public const string LoseOutcomeName = "Clash Lose Outcome";
        public const string LoseDamageName = "Clash Lose Damage";
        public const string DarkMessiahSoundsName = "Clash Dark Messiah Sounds";
        public const string HapticsName = "Clash Haptics Strength";

        public const bool DefaultScalesWithHeight = true;
        public const bool DefaultScalesWithGender = true;
        public const bool DefaultWinDisarms = true;
        public const bool DefaultDarkMessiahSounds = false;
        public const string DefaultLoseOutcome = ClashLoseOutcomes.DisarmAndHit;

        /// <summary>0 = the enemy barely pushes, 50 = some, 100 = twice 50, 200 = the player hardly has a chance (user 2026-11-18).</summary>
        public static readonly SliderSpec Difficulty = new SliderSpec(DifficultyName, 0f, 200f, 10f, 50f);

        /// <summary>
        /// Percent less force of a female enemy (user 2026-11-18, default 50). Literature: women have 40–60 % of men's upper-body
        /// strength (Roberts et al. 2016, doi 10.1139/apnm-2015-0540, after Wilmore 2008); men ~90 % more (Lassek &amp; Gaulin 2009).
        /// </summary>
        public static readonly SliderSpec FemaleStrength = new SliderSpec(FemaleStrengthName, 0f, 90f, 5f, 50f);

        /// <summary>Rounds of a lock: a random number between min and max (user 2026-11-18: 3–5, each 1–10).</summary>
        public static readonly SliderSpec MinRounds = new SliderSpec(MinRoundsName, 1f, 10f, 1f, 3f);

        public static readonly SliderSpec MaxRounds = new SliderSpec(MaxRoundsName, 1f, 10f, 1f, 5f);

        /// <summary>Rounds fewer against a smaller or a female enemy, while the matching scaling is on (a guess: 1).</summary>
        public static readonly SliderSpec FewerRounds = new SliderSpec(FewerRoundsName, 0f, 5f, 1f, 1f);

        /// <summary>The enemy's push at the top of its wave at difficulty 50, as metres the controller must lead the weapon (a guess).</summary>
        public static readonly SliderSpec EnemyPush = new SliderSpec(EnemyPushName, 0.01f, 0.2f, 0.01f, 0.06f);

        /// <summary>Lead of the controller that is only the heavy weapon's natural lag, not pushing (a guess).</summary>
        public static readonly SliderSpec DeadZone = new SliderSpec(DeadZoneName, 0f, 0.1f, 0.01f, 0.03f);

        /// <summary>Real seconds of one push phase; the enemy's push swells and ebbs once in it (a guess).</summary>
        public static readonly SliderSpec PushPhase = new SliderSpec(PushPhaseName, 0.5f, 5f, 0.25f, 2f);

        /// <summary>Real seconds to bring the controller back to the block point after a push phase (a guess).</summary>
        public static readonly SliderSpec ReturnWindow = new SliderSpec(ReturnWindowName, 0.25f, 3f, 0.25f, 1f);

        /// <summary>Metres between controller and weapon that count as back at the block point (a guess).</summary>
        public static readonly SliderSpec ReturnTolerance = new SliderSpec(ReturnToleranceName, 0.02f, 0.3f, 0.01f, 0.08f);

        public static readonly SliderSpec WinKneelChance = new SliderSpec(WinKneelChanceName, 0f, 100f, 5f, 25f);

        /// <summary>Health the player loses in a lost clash (the player has 100, Creature_PlayerDefault*.json).</summary>
        public static readonly SliderSpec LoseDamage = new SliderSpec(LoseDamageName, 0f, 100f, 5f, 20f);

        public static readonly SliderSpec Haptics = new SliderSpec(HapticsName, 0f, 1f, 0.1f, 0.6f);

        public static IEnumerable<SliderSpec> Sliders => new[]
        {
            Difficulty, FemaleStrength, MinRounds, MaxRounds, FewerRounds, EnemyPush, DeadZone, PushPhase, ReturnWindow,
            ReturnTolerance, WinKneelChance, LoseDamage, Haptics,
        };
    }

    /// <summary>Blade &amp; Sorcery's own effects for the lock (read in the catalog, Effects/*.json, game 1.3.2).</summary>
    public static class ClashFightSounds
    {
        /// <summary>Metal sparks and clang (Bas.AudioGroup.Collision.Metal); also Disarm Strike's grip spark.</summary>
        public const string SparkEffectId = "HitMaterialMetal";

        /// <summary>The metal rubbing loop (Bas.AudioGroup.Collision.Rubbing.Metal), loudness by intensity.</summary>
        public const string GrindEffectId = "RubbingMetalSound";
    }

    /// <summary>How hard an enemy pushes and how many rounds it lasts (pure values; the caller reads them from the game).</summary>
    public static class ClashStrength
    {
        /// <summary>
        /// Factor on the enemy's push: difficulty / 50 (linear), × the enemy's height over the player's (taller = stronger),
        /// × (1 − reduction) for a female enemy, each scaling only while switched on.
        /// </summary>
        public static float Factor(float difficulty, bool byHeight, float npcHeight, float playerHeight, bool byGender, bool female, float femaleReductionPercent)
        {
            var factor = Math.Max(0f, difficulty) / 50f;
            if (byHeight && npcHeight > 0f && playerHeight > 0f)
            {
                factor *= npcHeight / playerHeight;
            }
            if (byGender && female)
            {
                factor *= 1f - (Math.Min(100f, Math.Max(0f, femaleReductionPercent)) / 100f);
            }
            return factor;
        }

        /// <summary>A weaker enemy for the round count: smaller than the player (height scaling on) or female (gender scaling on).</summary>
        public static bool IsWeaker(bool byHeight, float npcHeight, float playerHeight, bool byGender, bool female) =>
            (byHeight && npcHeight > 0f && playerHeight > 0f && npcHeight < playerHeight) || (byGender && female);

        /// <summary>Rounds of one lock: random between min and max (roll 0..1), fewer against a weaker enemy, at least 1.</summary>
        public static int Rounds(int min, int max, double roll, bool weaker, int fewer)
        {
            var lo = Math.Max(1, Math.Min(min, max));
            var hi = Math.Max(lo, Math.Max(min, max));
            var r = Math.Min(0.999999, Math.Max(0.0, roll));
            var rounds = lo + (int)Math.Floor(r * (hi - lo + 1));
            if (weaker)
            {
                rounds -= Math.Max(0, fewer);
            }
            return Math.Max(1, rounds);
        }
    }

    public enum ClashPhase
    {
        /// <summary>The player pushes; the enemy's push swells and ebbs once.</summary>
        Push,

        /// <summary>The player must bring the controller back to the block point.</summary>
        Return,

        /// <summary>The lock is decided.</summary>
        Over,
    }

    public enum ClashStep
    {
        None,

        /// <summary>A push phase ended in the player's favour: the enemy pushes back, the return phase starts.</summary>
        PushHeld,

        /// <summary>The controller came back to the block point: the next push phase starts.</summary>
        Returned,

        /// <summary>The last push phase was held: the player wins.</summary>
        Won,

        /// <summary>The enemy pushed harder over a push phase: the player loses.</summary>
        LostPush,

        /// <summary>The controller did not come back to the block point in time: the player loses.</summary>
        LostReturn,
    }

    /// <summary>
    /// The rounds of one lock (user 2026-11-18). In each push phase the enemy's push swells and ebbs once (a half sine); the
    /// player holds the round when the summed lead of the controller (beyond the dead zone) is at least the enemy's summed
    /// push. Then the controller must come back to the block point within the return window, and the next push phase starts.
    /// The last held push phase wins. No progress bar: every round counts on its own.
    /// </summary>
    public sealed class ClashRounds
    {
        private readonly float _pushSeconds;
        private readonly float _returnSeconds;
        private readonly float _enemyPeak;
        private readonly float _deadZone;
        private readonly float _returnTolerance;
        private float _phaseTime;

        /// <summary>Float steps never add up exactly; a phase this close to its end is over.</summary>
        private const float Epsilon = 0.0001f;

        public ClashRounds(int rounds, float pushSeconds, float returnSeconds, float enemyPeak, float deadZone, float returnTolerance)
        {
            Rounds = Math.Max(1, rounds);
            _pushSeconds = Math.Max(0.01f, pushSeconds);
            _returnSeconds = Math.Max(0.01f, returnSeconds);
            _enemyPeak = Math.Max(0f, enemyPeak);
            _deadZone = Math.Max(0f, deadZone);
            _returnTolerance = Math.Max(0f, returnTolerance);
            Round = 1;
            Phase = ClashPhase.Push;
        }

        public int Rounds { get; }

        /// <summary>The current round, 1-based.</summary>
        public int Round { get; private set; }

        public ClashPhase Phase { get; private set; }

        /// <summary>The player's summed push (m·s) and the enemy's in the current or last push phase.</summary>
        public float PlayerSum { get; private set; }

        public float EnemySum { get; private set; }

        /// <summary>The enemy's push now (m): a half sine over the push phase; 0 outside it.</summary>
        public float EnemyPush => Phase == ClashPhase.Push ? EnemyPushAt(_phaseTime) : 0f;

        /// <summary>The enemy's push (m) at a time into a push phase.</summary>
        public float EnemyPushAt(float t) =>
            t <= 0f || t >= _pushSeconds ? 0f : _enemyPeak * (float)Math.Sin(Math.PI * t / _pushSeconds);

        /// <summary>The player's push from the controller's lead towards the enemy: what lies beyond the dead zone.</summary>
        public float PlayerPush(float lead) => Math.Max(0f, lead - _deadZone);

        /// <summary>
        /// One step of dt real seconds: lead = how far the controller is ahead of the weapon towards the enemy (m), gap = how
        /// far it is from the weapon at all (m).
        /// </summary>
        public ClashStep Tick(float dt, float lead, float gap)
        {
            if (Phase == ClashPhase.Over || dt <= 0f)
            {
                return ClashStep.None;
            }
            if (Phase == ClashPhase.Push)
            {
                var from = _phaseTime;
                var to = Math.Min(_pushSeconds, from + dt);
                var step = to - from;
                PlayerSum += PlayerPush(lead) * step;
                EnemySum += EnemyIntegral(from, to);
                _phaseTime = to;
                if (_phaseTime < _pushSeconds - Epsilon)
                {
                    return ClashStep.None;
                }
                if (PlayerSum < EnemySum)
                {
                    Phase = ClashPhase.Over;
                    return ClashStep.LostPush;
                }
                if (Round >= Rounds)
                {
                    Phase = ClashPhase.Over;
                    return ClashStep.Won;
                }
                Phase = ClashPhase.Return;
                _phaseTime = 0f;
                return ClashStep.PushHeld;
            }
            _phaseTime += dt;
            if (gap <= _returnTolerance)
            {
                Round++;
                Phase = ClashPhase.Push;
                _phaseTime = 0f;
                PlayerSum = 0f;
                EnemySum = 0f;
                return ClashStep.Returned;
            }
            if (_phaseTime >= _returnSeconds)
            {
                Phase = ClashPhase.Over;
                return ClashStep.LostReturn;
            }
            return ClashStep.None;
        }

        /// <summary>Exact integral of the half sine between two times of the push phase.</summary>
        private float EnemyIntegral(float from, float to)
        {
            var k = Math.PI / _pushSeconds;
            return (float)(_enemyPeak / k * (Math.Cos(k * from) - Math.Cos(k * to)));
        }
    }
}
