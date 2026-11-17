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
        public const string BlockReachName = "Clash Block Reach";
        public const string BlockAngleToleranceName = "Clash Block Angle Tolerance";

        public const bool DefaultOnlySwords = true;
        public const bool DefaultSlowMo = true;

        /// <summary>Seconds of wind-up from which a swing counts as big (harness probe 2026-11-17: plain swings 0.13–0.37 s, overhead/counter/lunge/step 0.40–0.77 s).</summary>
        public static readonly SliderSpec MinWindUp = new SliderSpec(MinWindUpName, 0.2f, 0.8f, 0.05f, 0.4f);

        /// <summary>World speed of the clash slow-motion (user 2026-11-17).</summary>
        public static readonly SliderSpec SlowMoScale = new SliderSpec(SlowMoScaleName, 0.1f, 0.8f, 0.05f, 0.3f);

        /// <summary>Metres between the NPC and the player within which the slow-motion can start (a guess).</summary>
        public static readonly SliderSpec NpcReach = new SliderSpec(NpcReachName, 1f, 4f, 0.25f, 2.5f);

        /// <summary>Metres from the flash point to the player's blade within which the slow-motion can start (a guess).</summary>
        public static readonly SliderSpec BlockReach = new SliderSpec(BlockReachName, 0.2f, 1.5f, 0.1f, 0.6f);

        /// <summary>Degrees the player's blade may be off a right angle to the swing (user 2026-11-17: very generous).</summary>
        public static readonly SliderSpec BlockAngleTolerance = new SliderSpec(BlockAngleToleranceName, 10f, 80f, 5f, 45f);

        /// <summary>Seconds after the strike starts in which the swing plane is read and the slow-motion decided.</summary>
        public const float JudgeWindow = 0.1f;

        /// <summary>Real seconds a clash slow-motion lasts at most (safety cap; it normally ends with the swing).</summary>
        public const float SlowMoMaxSeconds = 2.5f;

        /// <summary>Minimum speed (m/s) of the NPC's blade for its swing plane to be read.</summary>
        public const float MinBladeSpeed = 0.3f;

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

        public const string PointToleranceName = "Clash Point Tolerance";
        public const string WeaponWeightName = "Clash Weapon Weight";
        public const string MaxDurationName = "Clash Max Duration";

        /// <summary>Metres between the block's contact and the flash point within which the blades lock (a guess, headset).</summary>
        public static readonly SliderSpec PointTolerance = new SliderSpec(PointToleranceName, 0.05f, 0.6f, 0.05f, 0.25f);

        /// <summary>The player's weapon's mass factor during the lock (harness probe: ×20 sags the tip 5 cm held still; a guess).</summary>
        public static readonly SliderSpec WeaponWeight = new SliderSpec(WeaponWeightName, 1f, 50f, 1f, 20f);

        /// <summary>Real seconds a lock lasts at most (the NPC wins then, step 5c; a guess).</summary>
        public static readonly SliderSpec MaxDuration = new SliderSpec(MaxDurationName, 1f, 10f, 0.5f, 4f);

        public static IEnumerable<SliderSpec> Sliders => new[] { MinWindUp, SlowMoScale, NpcReach, BlockReach, BlockAngleTolerance, FlashSoundVolume, PointTolerance, WeaponWeight, MaxDuration };
    }

    public enum BlockVerdict
    {
        /// <summary>The blades lock: the clash starts.</summary>
        Clash,

        /// <summary>The block hit the blade away from the flash: an ordinary parry.</summary>
        OffPoint,

        /// <summary>At the flash, but the player's blade was not across the swing: an ordinary parry.</summary>
        WrongAngle,
    }

    public enum LockEnd
    {
        None,

        /// <summary>The lock ran its maximum time.</summary>
        TimeUp,

        /// <summary>Ended from outside: NPC dead or gone, weapon dropped, feature off, level unload.</summary>
        Broken,
    }

    /// <summary>One clash lock: started, ticked until its maximum time, or broken; diagnostics every 0.1 s.</summary>
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

        public LockEnd Tick(float now, float maxDuration)
        {
            if (!IsLocked || now - _startedAt < maxDuration)
            {
                return LockEnd.None;
            }
            IsLocked = false;
            return LockEnd.TimeUp;
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

    public enum SlowMoVerdict
    {
        Go,
        NpcTooFar,
        BladeTooFar,
        NoSwingPlane,
        WrongAngle,
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

        /// <summary>A block (the NPC's swing hit the player's weapon) at the flash point and across the swing locks the blades; a block whose swing plane is unknown is decided by the contact point alone.</summary>
        public static BlockVerdict JudgeBlock(float contactToFlash, float pointTolerance, float? angleToNormal, float angleTolerance)
        {
            if (contactToFlash > pointTolerance)
            {
                return BlockVerdict.OffPoint;
            }
            return !angleToNormal.HasValue || angleToNormal.Value <= angleTolerance ? BlockVerdict.Clash : BlockVerdict.WrongAngle;
        }

        public static bool CanFlash(bool onlySwords, bool isSword, bool hasSlashBlade) => hasSlashBlade && (!onlySwords || isSword);

        public static bool IsBigSwing(bool isThrust, float windUpSeconds, float minWindUp) => !isThrust && windUpSeconds >= minWindUp;

        /// <summary>Whether the slow-motion starts; angleToNormal = degrees between the player's blade and the swing plane's normal, null = plane not known.</summary>
        public static SlowMoVerdict JudgeSlowMo(float npcDistance, float npcReach, float bladeToFlash, float blockReach, float? angleToNormal, float tolerance)
        {
            if (npcDistance > npcReach)
            {
                return SlowMoVerdict.NpcTooFar;
            }
            if (!(bladeToFlash <= blockReach))
            {
                return SlowMoVerdict.BladeTooFar;
            }
            if (!angleToNormal.HasValue)
            {
                return SlowMoVerdict.NoSwingPlane;
            }
            return angleToNormal.Value <= tolerance ? SlowMoVerdict.Go : SlowMoVerdict.WrongAngle;
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
        /// Normal of the plane the NPC's blade swings in (its axis and its motion); null when the blade is slower than
        /// <paramref name="minSpeed"/> or moves along itself (a thrust has no swing plane).
        /// </summary>
        public static Vec3? SwingPlaneNormal(Vec3 bladeAxis, Vec3 bladeVelocity, float minSpeed)
        {
            if (bladeVelocity.Length < minSpeed)
            {
                return null;
            }
            var normal = Vec3.Cross(bladeAxis.Normalized(), bladeVelocity.Normalized());
            return normal.Length < 0.3f ? (Vec3?)null : normal.Normalized();
        }

        /// <summary>Degrees (0–90) between the player's blade and the swing plane's normal, either way round: 0 = right across the swing.</summary>
        public static float AngleToNormal(Vec3 playerBladeAxis, Vec3 normal)
        {
            var cos = Math.Abs(Vec3.Dot(playerBladeAxis.Normalized(), normal.Normalized()));
            return (float)(Math.Acos(Math.Min(1.0, cos)) * 180.0 / Math.PI);
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

        /// <summary>The window ran out without a slow-motion: the last reason, once (NoSwingPlane when nothing was judged).</summary>
        public SlowMoVerdict? JudgingOver(TId npc, float now)
        {
            if (!_swings.TryGetValue(npc, out var s) || !s.StrikeAt.HasValue || s.Judged || now - s.StrikeAt.Value <= _window)
            {
                return null;
            }
            s.Judged = true;
            return s.Last ?? SlowMoVerdict.NoSwingPlane;
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
