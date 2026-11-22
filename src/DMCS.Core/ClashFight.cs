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
        public const string WinDisarmsName = "Clash Win Disarms";
        public const string WinKneelChanceName = "Clash Win Kneel Chance";
        public const string LoseOutcomeName = "Clash Lose Outcome";
        public const string LoseDamageName = "Clash Lose Damage";
        public const string DarkMessiahSoundsName = "Clash Dark Messiah Sounds";
        public const string HapticsName = "Clash Haptics Strength";
        public const string DuelStarName = "Clash Duel Star";
        public const string OutcomeCuesName = "Clash Outcome Cues";

        /// <summary>The star at the crossed blades shows who is ahead in the round (user 2026-11-22).</summary>
        public const bool DefaultDuelStar = true;

        /// <summary>Win and loss each get their own sparks, sound and vibration (user 2026-11-22).</summary>
        public const bool DefaultOutcomeCues = true;

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

        /// <summary>Real seconds of one push round; the enemy's push swells and ebbs once in it (a guess).</summary>
        public static readonly SliderSpec PushPhase = new SliderSpec(PushPhaseName, 0.5f, 5f, 0.25f, 2f);

        public static readonly SliderSpec WinKneelChance = new SliderSpec(WinKneelChanceName, 0f, 100f, 5f, 25f);

        /// <summary>Health the player loses in a lost clash (the player has 100, Creature_PlayerDefault*.json).</summary>
        public static readonly SliderSpec LoseDamage = new SliderSpec(LoseDamageName, 0f, 100f, 5f, 20f);

        public static readonly SliderSpec Haptics = new SliderSpec(HapticsName, 0f, 1f, 0.1f, 0.6f);

        public static IEnumerable<SliderSpec> Sliders => new[]
        {
            Difficulty, FemaleStrength, MinRounds, MaxRounds, FewerRounds, EnemyPush, DeadZone, PushPhase, WinKneelChance,
            LoseDamage, Haptics,
        };
    }

    /// <summary>Blade &amp; Sorcery's own effects for the lock (read in the catalog, Effects/*.json, game 1.3.2).</summary>
    public static class ClashFightSounds
    {
        /// <summary>
        /// Metal sparks and clang (Bas.AudioGroup.Collision.Metal); also Disarm Strike's grip spark. Played without its
        /// rubbing loop (OneShotEffect). The lock has no grinding sound (removed by the user 2026-11-21).
        /// </summary>
        public const string SparkEffectId = "HitMaterialMetal";

        /// <summary>The arena's melee-kill stinger (Bas.Audio.Stinger.OnKillMele): the sound of a won duel (user 2026-11-22).</summary>
        public const string WinEffectId = "ArenaOnKillStingerMele";

        /// <summary>A heavy body fall (Bas.AudioGroup.Ragdoll.FallIntensity3): the dull thud of a lost duel (user 2026-11-22).</summary>
        public const string LoseEffectId = "Fall";

        /// <summary>Sparks thrown around the crossed blades when the duel is won.</summary>
        public const int WinSparks = 6;

        /// <summary>Metres around the lock point the win sparks are thrown.</summary>
        public const float WinSparkSpread = 0.12f;
    }

    /// <summary>
    /// What the player sees and feels in a blade lock (user 2026-11-22: "unclear whether you win or lose"). The star at the
    /// crossed blades shows who is ahead in the round: warm white when even (the flash's own colour), green and bigger when
    /// the player is ahead, red and smaller when the enemy is. Win and loss each get their own vibration.
    /// </summary>
    public static class ClashDuelSignal
    {
        private static readonly (float R, float G, float B) Even = (1f, 0.92f, 0.7f);
        private static readonly (float R, float G, float B) Ahead = (0.3f, 1f, 0.35f);
        private static readonly (float R, float G, float B) Behind = (1f, 0.2f, 0.15f);

        /// <summary>Size factor of the star when fully ahead and fully behind.</summary>
        private const float AheadSize = 1.4f;
        private const float BehindSize = 0.7f;

        /// <summary>The star's colour for an advantage from −1 (the enemy is ahead) to 1 (the player is).</summary>
        public static (float R, float G, float B) Tint(float advantage)
        {
            var a = Clamp(advantage);
            var to = a >= 0f ? Ahead : Behind;
            var t = Math.Abs(a);
            return (Lerp(Even.R, to.R, t), Lerp(Even.G, to.G, t), Lerp(Even.B, to.B, t));
        }

        /// <summary>The star's size factor (1 when even).</summary>
        public static float Size(float advantage)
        {
            var a = Clamp(advantage);
            return a >= 0f ? Lerp(1f, AheadSize, a) : Lerp(1f, BehindSize, -a);
        }

        /// <summary>Vibration ticks (seconds after the end, strength 0–1): three strong quick beats for a win, a longer weaker rumble for a loss.</summary>
        public static IReadOnlyList<(float Time, float Strength)> Haptics(bool won)
        {
            var ticks = new List<(float Time, float Strength)>();
            if (won)
            {
                for (var i = 0; i < 3; i++)
                {
                    ticks.Add((i * 0.1f, 1f));
                }
                return ticks;
            }
            for (var t = 0f; t <= 0.5f; t += 0.04f)
            {
                ticks.Add((t, 0.7f));
            }
            return ticks;
        }

        private static float Clamp(float v) => Math.Max(-1f, Math.Min(1f, v));

        private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
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
        /// <summary>The player pushes; the enemy's push swells and ebbs once per round.</summary>
        Push,

        /// <summary>The lock is decided.</summary>
        Over,
    }

    public enum ClashStep
    {
        None,

        /// <summary>A round ended in the player's favour: the next round starts at once (no return phase since 2026-11-22).</summary>
        PushHeld,

        /// <summary>The last round was held: the player wins.</summary>
        Won,

        /// <summary>The enemy pushed harder over a round: the player loses.</summary>
        LostPush,
    }

    /// <summary>
    /// The rounds of one lock (user 2026-11-18; return phase dropped 2026-11-22). In each round the enemy's push swells and
    /// ebbs once (a half sine); the player holds the round when the summed lead of the controller (beyond the dead zone) is
    /// at least the enemy's summed push, and the next round starts at once. The last held round wins. Every round counts on
    /// its own; <see cref="Advantage"/> says who is ahead in the current one.
    /// </summary>
    public sealed class ClashRounds
    {
        private readonly float _pushSeconds;
        private readonly float _enemyPeak;
        private readonly float _deadZone;
        private float _phaseTime;

        /// <summary>Float steps never add up exactly; a round this close to its end is over.</summary>
        private const float Epsilon = 0.0001f;

        public ClashRounds(int rounds, float pushSeconds, float enemyPeak, float deadZone)
        {
            Rounds = Math.Max(1, rounds);
            _pushSeconds = Math.Max(0.01f, pushSeconds);
            _enemyPeak = Math.Max(0f, enemyPeak);
            _deadZone = Math.Max(0f, deadZone);
            Round = 1;
            Phase = ClashPhase.Push;
        }

        /// <summary>The player's and the enemy's sums of the round that ended last (for the log).</summary>
        public float LastPlayerSum { get; private set; }

        public float LastEnemySum { get; private set; }

        /// <summary>
        /// Who is ahead in the current round, from −1 (the enemy) to 1 (the player): the difference of the sums so far over
        /// what the enemy pushes in a whole round. 0 at the start of every round; with no enemy push, any push is fully ahead.
        /// </summary>
        public float Advantage
        {
            get
            {
                var whole = (float)(2.0 * _enemyPeak * _pushSeconds / Math.PI);
                if (whole <= Epsilon)
                {
                    return PlayerSum > 0f ? 1f : 0f;
                }
                return Math.Max(-1f, Math.Min(1f, (PlayerSum - EnemySum) / whole));
            }
        }

        public int Rounds { get; }

        /// <summary>The current round, 1-based.</summary>
        public int Round { get; private set; }

        public ClashPhase Phase { get; private set; }

        /// <summary>The player's summed push (m·s) and the enemy's in the current round.</summary>
        public float PlayerSum { get; private set; }

        public float EnemySum { get; private set; }

        /// <summary>The enemy's push now (m): a half sine over the round; 0 once the lock is decided.</summary>
        public float EnemyPush => Phase == ClashPhase.Push ? EnemyPushAt(_phaseTime) : 0f;

        /// <summary>The enemy's push (m) at a time into a push phase.</summary>
        public float EnemyPushAt(float t) =>
            t <= 0f || t >= _pushSeconds ? 0f : _enemyPeak * (float)Math.Sin(Math.PI * t / _pushSeconds);

        /// <summary>The player's push from the controller's lead towards the enemy: what lies beyond the dead zone.</summary>
        public float PlayerPush(float lead) => Math.Max(0f, lead - _deadZone);

        /// <summary>One step of dt real seconds; lead = how far the controller pushes ahead of the held weapon (m).</summary>
        public ClashStep Tick(float dt, float lead)
        {
            if (Phase == ClashPhase.Over || dt <= 0f)
            {
                return ClashStep.None;
            }
            var from = _phaseTime;
            var to = Math.Min(_pushSeconds, from + dt);
            PlayerSum += PlayerPush(lead) * (to - from);
            EnemySum += EnemyIntegral(from, to);
            _phaseTime = to;
            if (_phaseTime < _pushSeconds - Epsilon)
            {
                return ClashStep.None;
            }
            LastPlayerSum = PlayerSum;
            LastEnemySum = EnemySum;
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
            Round++;
            _phaseTime = 0f;
            PlayerSum = 0f;
            EnemySum = 0f;
            return ClashStep.PushHeld;
        }

        /// <summary>Exact integral of the half sine between two times of the push phase.</summary>
        private float EnemyIntegral(float from, float to)
        {
            var k = Math.PI / _pushSeconds;
            return (float)(_enemyPeak / k * (Math.Cos(k * from) - Math.Cos(k * to)));
        }
    }
}
