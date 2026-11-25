using System;
using System.Collections.Generic;
using System.Linq;

namespace DMCS.Core
{
    /// <summary>Clash of Might's Mod Menu names, ranges and defaults (single source of truth).</summary>
    public static class ClashSettings
    {
        public const string OnlySwordsName = "Clash Only Swords";
        public const string MinWindUpName = "Clash Min Wind Up";
        public const string SlowMoName = "Clash Slow Motion";
        public const string SlowMoScaleName = "Clash Slow Motion Scale";
        public const string NpcReachName = "Clash Enemy Reach";

        public const bool DefaultOnlySwords = true;
        public const bool DefaultSlowMo = true;

        /// <summary>Seconds of wind-up from which a swing counts as big (harness probe 2026-11-17: plain swings 0.13–0.37 s, overhead/counter/lunge/step 0.40–0.77 s).</summary>
        public static readonly SliderSpec MinWindUp = new SliderSpec(MinWindUpName, 0.2f, 0.8f, 0.05f, 0.45f);

        /// <summary>World speed of the clash slow-motion (user 2026-11-17).</summary>
        public static readonly SliderSpec SlowMoScale = new SliderSpec(SlowMoScaleName, 0.1f, 0.8f, 0.05f, 0.3f);

        /// <summary>Metres between the NPC and the player within which the slow-motion can start (a guess).</summary>
        public static readonly SliderSpec NpcReach = new SliderSpec(NpcReachName, 1f, 4f, 0.25f, 2.5f);

        /// <summary>Seconds after the strike starts in which the slow-motion is decided.</summary>
        public const float JudgeWindow = 0.1f;

        /// <summary>Real seconds a clash slow-motion lasts at most (safety cap; it normally ends with the swing).</summary>
        public const float SlowMoMaxSeconds = 2.5f;

        public const string FlashSoundName = "Clash Flash Sound";
        public const string FlashGameSoundName = "Clash Flash Game Sound";
        public const string FlashSoundVolumeName = "Clash Flash Sound Volume";

        /// <summary>DMCS 1 = modelled on Sekiro's perilous-attack cue (user 2026-11-17).</summary>
        public const string DefaultFlashSound = "DMCS 1";

        public const string DefaultFlashGameSound = FlashSound.Off;

        public static readonly SliderSpec FlashSoundVolume = new SliderSpec(FlashSoundVolumeName, 0f, 1f, 0.05f, 0.8f);

        /// <summary>The game's own catalog effects that can sound with the flash (any combination with a DMCS sound).</summary>
        public static readonly string[] GameSounds = { FlashSound.Off, "Metal Hit", "Focus Ready", "Gravity Pulse" };

        /// <summary>Catalog effect id of a game sound name; null for Off or unknown. Metal Hit also sparks (Effect_HitMaterialMetal).</summary>
        public static string GameSoundEffect(string name)
        {
            switch (name)
            {
                case "Metal Hit": return "HitMaterialMetal";
                case "Focus Ready": return "FocusReady";
                case "Gravity Pulse": return "ImpactPulse";
                default: return null;
            }
        }

        public const string FlashAtTopName = "Clash Flash At Swing Top";

        /// <summary>The flash appears at the top of the wind-up (user 2026-11-17, default on); off: when the wind-up starts.</summary>
        public const bool DefaultFlashAtTop = true;

        public const string WeaponWeightName = "Clash Weapon Weight";

        /// <summary>The player's weapon's mass factor during the lock (harness probe: ×20 sags the tip 5 cm held still; a guess).</summary>
        public static readonly SliderSpec WeaponWeight = new SliderSpec(WeaponWeightName, 1f, 50f, 1f, 20f);

        public const string HoldBladeName = "Clash Hold Blade";
        public const string HoldSlackName = "Clash Hold Slack";

        /// <summary>
        /// The player's blade stays where the blades met for the whole lock (user 2026-11-21: a swing carried it past the
        /// lock point; Player.log 2026-11-20, seven locks: up to 0.27–1.14 m off, mostly 0.2 s after the lock start, and 0.2–2.2 s to come back).
        /// </summary>
        public const bool DefaultHoldBlade = true;

        /// <summary>Metres the held blade moves freely around the lock point before the hold catches it (a guess, headset).</summary>
        public static readonly SliderSpec HoldSlack = new SliderSpec(HoldSlackName, 0f, 0.1f, 0.01f, 0.02f);

        public const string HoldStyleName = "Clash Hold Style";
        public const string HoldSpringName = "Clash Hold Spring Strength";
        public const string HoldSpringDampingName = "Clash Hold Spring Damping";
        public const string HoldRotationName = "Clash Hold Rotation";
        public const string HoldBrieflyName = "Clash Hold Briefly";
        public const string HoldTimeName = "Clash Hold Time";

        /// <summary>Spring: the user expects it to feel better than Hard (2026-11-22); both stay selectable.</summary>
        public const string DefaultHoldStyle = ClashHold.Spring;

        /// <summary>The spring's natural frequency in Hz (higher = firmer), independent of the blade's mass (a guess, headset).</summary>
        public static readonly SliderSpec HoldSpring = new SliderSpec(HoldSpringName, 1f, 10f, 0.5f, 3f);

        /// <summary>The spring's damping ratio: 0 bounces on, 1 settles without a bounce (a guess, headset).</summary>
        public static readonly SliderSpec HoldSpringDamping = new SliderSpec(HoldSpringDampingName, 0f, 1f, 0.1f, 0.5f);

        /// <summary>Off: the held blade still turns around the lock point; on: it cannot (firm in both styles).</summary>
        public const bool DefaultHoldRotation = false;

        /// <summary>Off: the hold lasts the whole lock; on: only the first Hold Time, then the weapon's weight alone.</summary>
        public const bool DefaultHoldBriefly = false;

        /// <summary>Seconds the hold lasts with Hold Briefly: long enough to catch the swing (it peaked about 0.2 s in, 2026-11-20 log).</summary>
        public static readonly SliderSpec HoldTime = new SliderSpec(HoldTimeName, 0.1f, 1f, 0.05f, 0.3f);

        public static IEnumerable<SliderSpec> Sliders => new[] { MinWindUp, SlowMoScale, NpcReach, FlashSoundVolume, WeaponWeight, HoldSlack, HoldSpring, HoldSpringDamping, HoldTime };
    }

    public enum BlockVerdict
    {
        /// <summary>Both hands on the weapon and blade on blade: the blades lock, the clash starts (user 2026-11-24).</summary>
        Clash,

        /// <summary>One hand on the weapon: an ordinary parry.</summary>
        OneHanded,

        /// <summary>Both hands, but the blades did not meet (a hilt, guard or handle took the blow): an ordinary parry.</summary>
        NotBladeOnBlade,
    }

    public enum LockEnd
    {
        None,

        /// <summary>The rounds decided the lock (won or lost).</summary>
        Decided,

        /// <summary>Ended from outside: NPC dead or gone, weapon dropped, feature off, level unload.</summary>
        Broken,
    }

    /// <summary>One clash lock: started, then decided by its rounds or broken from outside; diagnostics every 0.1 s.</summary>
    public sealed class ClashLock
    {
        private const float DiagnosticEvery = 0.1f;
        private float _startedAt;
        private float _nextDiagnostic;

        public bool IsLocked { get; private set; }

        public void Start(float now)
        {
            IsLocked = true;
            _startedAt = now;
            _nextDiagnostic = now;
        }

        public float Elapsed(float now) => now - _startedAt;

        public LockEnd Decide()
        {
            if (!IsLocked)
            {
                return LockEnd.None;
            }
            IsLocked = false;
            return LockEnd.Decided;
        }

        public LockEnd Break()
        {
            if (!IsLocked)
            {
                return LockEnd.None;
            }
            IsLocked = false;
            return LockEnd.Broken;
        }

        public bool DiagnosticDue(float now)
        {
            if (!IsLocked || now < _nextDiagnostic - 0.0001f)
            {
                return false;
            }
            _nextDiagnostic = now + DiagnosticEvery;
            return true;
        }
    }

    /// <summary>
    /// The top of a wind-up, where the flash appears (user 2026-11-17: like sunlight caught when the blade is raised all the
    /// way). Harness probe 2026-11-17, 11 big swings: the blade tip is highest at 79–100 % of the wind-up (0.03–0.08 s before
    /// the strike), except a lunge that starts high (0–12 %). So: the tip's height has fallen 2 cm below its highest point,
    /// not before 40 % of the wind-up; at the latest when the wind-up is over.
    /// </summary>
    public sealed class SwingTop
    {
        public const float Drop = 0.02f;
        public const float EarliestShare = 0.4f;

        private readonly float _windUp;
        private float _highest = float.NegativeInfinity;

        public SwingTop(float windUpSeconds)
        {
            _windUp = windUpSeconds;
        }

        public bool Reached { get; private set; }

        /// <summary>One sample (seconds since the wind-up started, tip height); true once, when the top is reached.</summary>
        public bool Sample(float elapsed, float height)
        {
            if (Reached)
            {
                return false;
            }
            _highest = Math.Max(_highest, height);
            if (elapsed >= _windUp || (elapsed >= _windUp * EarliestShare && height <= _highest - Drop))
            {
                Reached = true;
                return true;
            }
            return false;
        }
    }

    public enum SlowMoVerdict
    {
        Go,
        NpcTooFar,

        /// <summary>The player does not hold a weapon with both hands.</summary>
        OneHanded,

        /// <summary>The judge window ran out before anything was judged.</summary>
        NotJudged,
    }

    /// <summary>Step 5 (Clash of Might) decisions over plain values.</summary>
    public static class ClashRules
    {
        /// <summary>A sword by the catalog: category Swords or Greatswords, or an item id starting with "Sword" (the faction swords).</summary>
        public static bool IsSword(string category, string itemId)
        {
            var c = category ?? "";
            return c.Equals("Swords", StringComparison.OrdinalIgnoreCase)
                || c.Equals("Greatswords", StringComparison.OrdinalIgnoreCase)
                || (itemId ?? "").StartsWith("Sword", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the running slow-motion is still the clash's own, so ending it does not stop another one (the player's
        /// Focus, a critical). While the game still eases the scale down (TimeManager state Starting) it is ours: the scale
        /// is not at our value yet. Once running, only at our scale.
        /// </summary>
        public static bool StillOurSlowMotion(bool starting, float timeScale, float ourScale) =>
            starting || Math.Abs(timeScale - ourScale) <= 0.01f;

        /// <summary>
        /// A block of the flashing blade (the NPC's swing hit the player's weapon) locks the blades when the player holds
        /// that weapon with both hands and the two blades met, blade on blade (user 2026-11-24); otherwise it is an ordinary
        /// parry. One hand is named first. Angle, hand speed and where along the blades they met do not count.
        /// </summary>
        public static BlockVerdict JudgeBlock(bool twoHanded, bool bladesMeet)
        {
            if (!twoHanded)
            {
                return BlockVerdict.OneHanded;
            }
            return bladesMeet ? BlockVerdict.Clash : BlockVerdict.NotBladeOnBlade;
        }

        public static bool CanFlash(bool onlySwords, bool isSword, bool hasSlashBlade) => hasSlashBlade && (!onlySwords || isSword);

        public static bool IsBigSwing(bool isThrust, float windUpSeconds, float minWindUp) => !isThrust && windUpSeconds >= minWindUp;

        /// <summary>
        /// A thrust never flashes: the game labels it so, or its animation's own name (the last part of its address) starts
        /// with "Stab". The game's data labels one stab a swing (1.3.2: CounterThrust in the one-handed sword stance,
        /// animation Bas.Animation.Attack.Sword1h.Counter.Stab, wind-up 0.76 s; it flashed in the headset 2026-11-22). The
        /// game's nine stabs all have such a name, no swing has.
        /// </summary>
        public static bool IsThrust(bool labelledThrust, string animationAddress)
        {
            if (labelledThrust)
            {
                return true;
            }
            var address = animationAddress ?? "";
            var name = address.Substring(address.LastIndexOf('.') + 1);
            return name.StartsWith("Stab", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the slow-motion starts at the strike: the NPC within its swing reach of the player and the player
        /// holding a weapon with both hands, ready for the clash's only condition (user 2026-11-24).
        /// </summary>
        public static SlowMoVerdict JudgeSlowMo(float npcDistance, float npcReach, bool twoHanded)
        {
            if (npcDistance > npcReach)
            {
                return SlowMoVerdict.NpcTooFar;
            }
            return twoHanded ? SlowMoVerdict.Go : SlowMoVerdict.OneHanded;
        }
    }

    /// <summary>A plain 3D vector for the geometry (Core has no Unity).</summary>
    public readonly struct Vec3
    {
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);

        public float Length => (float)Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public static float Dot(Vec3 a, Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

        public Vec3 Normalized()
        {
            var l = Length;
            return l > 0f ? this * (1f / l) : Zero;
        }
    }

    public static class ClashGeometry
    {
        /// <summary>
        /// The push (user 2026-11-24, "Controller-Weg"): how far the player moved the real controllers since the blades
        /// locked, the mean of now − start over every hand on the weapon (pairs by index; a missing one is left out). The
        /// in-game hand is no input: the weight, the hold and the frozen enemy blade moved it 0.1–0.5 m in the first second
        /// of a lock and that read as a push nobody made. Two controllers moving apart cancel; both forward is the push; one
        /// forward counts half.
        /// </summary>
        public static Vec3 ControllerTravel(IReadOnlyList<Vec3> starts, IReadOnlyList<Vec3> nows)
        {
            var count = Math.Min(starts?.Count ?? 0, nows?.Count ?? 0);
            if (count == 0)
            {
                return Vec3.Zero;
            }
            var sum = Vec3.Zero;
            for (var i = 0; i < count; i++)
            {
                sum = sum + (nows[i] - starts[i]);
            }
            return sum * (1f / count);
        }

        /// <summary>
        /// A push vector along the line from the head to the lock point (m): pushing the
        /// crossed blades away from oneself counts, wherever they crossed (above, beside or in front); pulling back counts
        /// against. 2026-11-22: replaces "level towards the enemy's body", which missed most of the user's pushes.
        /// </summary>
        public static float PushLead(Vec3 handAhead, Vec3 head, Vec3 lockPoint)
        {
            var line = (lockPoint - head).Normalized();
            return Vec3.Dot(handAhead, line);
        }

        /// <summary>
        /// The lead split into forward (along head → lock point, as <see cref="PushLead"/>) and sideways (along the player's
        /// right, made square to that line; positive = right, negative = left). User 2026-11-22: the push can go sideways.
        /// </summary>
        public static (float Forward, float Side) PushComponents(Vec3 handAhead, Vec3 head, Vec3 lockPoint, Vec3 headRight)
        {
            var line = (lockPoint - head).Normalized();
            var right = (headRight - (line * Vec3.Dot(headRight, line))).Normalized();
            return (Vec3.Dot(handAhead, line), Vec3.Dot(handAhead, right));
        }

        public static float DistanceToSegment(Vec3 point, Vec3 a, Vec3 b)
        {
            var ab = b - a;
            var lengthSq = Vec3.Dot(ab, ab);
            var t = lengthSq > 0f ? Math.Max(0f, Math.Min(1f, Vec3.Dot(point - a, ab) / lengthSq)) : 0f;
            return (point - (a + (ab * t))).Length;
        }

        /// <summary>Middle of the blade along its axis, from the projections of its collider corners onto the axis; null without any.</summary>
        public static float? MiddleOfExtent(IEnumerable<float> projections)
        {
            var list = projections.ToList();
            if (list.Count == 0)
            {
                return null;
            }
            return (list.Min() + list.Max()) / 2f;
        }
    }

    /// <summary>
    /// The telegraph of a big swing, per attacking NPC: wind-up → flash; strike start → for a short window the slow-motion
    /// is judged each physics step (the swing plane is only known once the blade moves forward; the wind-up moves in
    /// another plane, harness probe 2026-11-17); end of the attack → flash off. One slow-motion at a time.
    /// </summary>
    public sealed class ClashTelegraph<TId>
    {
        private sealed class Swing
        {
            public float? StrikeAt;
            public bool SlowMo;
            public bool Judged;
            public SlowMoVerdict? Last;
        }

        private readonly float _window;
        private readonly Dictionary<TId, Swing> _swings = new Dictionary<TId, Swing>();

        public ClashTelegraph(float judgeWindow)
        {
            _window = judgeWindow;
        }

        public bool AnySlowMo => _swings.Values.Any(s => s.SlowMo);

        /// <summary>A big swing winds up: it flashes (a new wind-up replaces the NPC's earlier swing).</summary>
        public void WindUp(TId npc) => _swings[npc] = new Swing();

        public bool IsFlashing(TId npc) => _swings.ContainsKey(npc);

        public bool InSlowMo(TId npc) => _swings.TryGetValue(npc, out var s) && s.SlowMo;

        public void StrikeStarted(TId npc, float now)
        {
            if (_swings.TryGetValue(npc, out var s) && !s.StrikeAt.HasValue)
            {
                s.StrikeAt = now;
            }
        }

        public bool IsJudging(TId npc, float now) =>
            _swings.TryGetValue(npc, out var s) && s.StrikeAt.HasValue && !s.Judged && now - s.StrikeAt.Value <= _window;

        /// <summary>One physics step's verdict; true when the slow-motion starts now.</summary>
        public bool Verdict(TId npc, SlowMoVerdict verdict, float now)
        {
            if (!IsJudging(npc, now))
            {
                return false;
            }
            var s = _swings[npc];
            s.Last = verdict;
            if (verdict != SlowMoVerdict.Go || AnySlowMo)
            {
                return false;
            }
            s.SlowMo = true;
            s.Judged = true;
            return true;
        }

        /// <summary>The window ran out without a slow-motion: the last reason, once (NotJudged when nothing was judged).</summary>
        public SlowMoVerdict? JudgingOver(TId npc, float now)
        {
            if (!_swings.TryGetValue(npc, out var s) || !s.StrikeAt.HasValue || s.Judged || now - s.StrikeAt.Value <= _window)
            {
                return null;
            }
            s.Judged = true;
            return s.Last ?? SlowMoVerdict.NotJudged;
        }

        /// <summary>The attack ended or was cancelled: flash off; true when its slow-motion was running.</summary>
        public bool Ended(TId npc)
        {
            if (!_swings.TryGetValue(npc, out var s))
            {
                return false;
            }
            _swings.Remove(npc);
            return s.SlowMo;
        }

        /// <summary>Forgets every swing; returns the NPCs that were flashing so the caller can clean up.</summary>
        public List<TId> Clear()
        {
            var all = _swings.Keys.ToList();
            _swings.Clear();
            return all;
        }
    }
}
