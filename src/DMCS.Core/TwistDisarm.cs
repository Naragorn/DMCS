using System;
using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>Twist Disarm's Mod Menu names, ranges and defaults (spec: Disarm Strike 2b, built with step 5).</summary>
    public static class TwistSettings
    {
        public const string EnabledName = "Twist Disarm";
        public const string ChanceName = "Twist Disarm Chance";
        public const string MinRollSpeedName = "Twist Min Roll Speed";

        /// <summary>Spec: default on, chance 50 %, no stagger needed.</summary>
        public const bool DefaultEnabled = true;

        public static readonly SliderSpec Chance = new SliderSpec(ChanceName, 0f, 100f, 5f, 50f);

        /// <summary>
        /// Degrees per second the blade must roll around its own long axis (a wrist twist) when it meets the enemy's
        /// weapon. A guess for the headset: a quick turn of a screwdriver is a few hundred °/s, a flick of the wrist more.
        /// </summary>
        public static readonly SliderSpec MinRollSpeed = new SliderSpec(MinRollSpeedName, 100f, 1500f, 50f, 400f);

        /// <summary>Seconds before the contact in which the fastest roll counts (the twist comes just before or with the hit).</summary>
        public const float RollWindow = 0.15f;

        /// <summary>Seconds of controller rotation one roll measurement spans (frames come faster than the tracking).</summary>
        public const float MeterWindow = 0.03f;

        /// <summary>
        /// Seconds one blade contact counts once per enemy: a single clash of the blades raises several collision events.
        /// </summary>
        public const float ContactCooldown = 0.5f;

        public static IEnumerable<SliderSpec> Sliders => new[] { Chance, MinRollSpeed };
    }

    /// <summary>A rotation (unit quaternion), as Unity's: x, y, z, w.</summary>
    public readonly struct Quat
    {
        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float W { get; }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        public Quat Conjugate => new Quat(-X, -Y, -Z, W);

        public static Quat operator *(Quat a, Quat b) => new Quat(
            (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
            (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
            (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W),
            (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));

        /// <summary>A rotation of the given degrees around an axis.</summary>
        public static Quat AngleAxis(float degrees, Vec3 axis)
        {
            var n = axis.Normalized();
            var half = degrees * Math.PI / 360.0;
            var s = (float)Math.Sin(half);
            return new Quat(n.X * s, n.Y * s, n.Z * s, (float)Math.Cos(half));
        }

        /// <summary>
        /// The angular velocity (degrees per second, as an axis × rate vector in the parent frame) that turns
        /// <paramref name="from"/> into <paramref name="to"/> in <paramref name="seconds"/>; the shorter way round.
        /// </summary>
        public static Vec3 AngularVelocity(Quat from, Quat to, float seconds)
        {
            if (seconds <= 0f)
            {
                return Vec3.Zero;
            }
            var d = to * from.Conjugate;
            if (d.W < 0f)
            {
                d = new Quat(-d.X, -d.Y, -d.Z, -d.W);
            }
            var sinHalf = Math.Sqrt((d.X * d.X) + (d.Y * d.Y) + (d.Z * d.Z));
            if (sinHalf < 1e-7)
            {
                return Vec3.Zero;
            }
            var angle = 2.0 * Math.Atan2(sinHalf, d.W) * 180.0 / Math.PI;
            var scale = (float)(angle / seconds / sinHalf);
            return new Vec3(d.X * scale, d.Y * scale, d.Z * scale);
        }
    }

    /// <summary>
    /// How fast a hand rolls around an axis (°/s): the controller's rotation sampled every frame (rig-local, so turning
    /// the body does not count), measured over a short span; the caller projects it on the held blade's long axis.
    /// </summary>
    public sealed class RollMeter
    {
        private readonly float _window;
        private readonly Queue<(float Time, Quat Rotation)> _samples = new Queue<(float Time, Quat Rotation)>();

        public RollMeter(float window)
        {
            _window = window;
        }

        /// <summary>Adds a sample; returns the angular velocity (°/s vector) over at least the window, or null before that.</summary>
        public Vec3? Add(float time, Quat rotation)
        {
            _samples.Enqueue((time, rotation));
            (float Time, Quat Rotation)? oldest = null;
            while (_samples.Count > 1 && time - _samples.Peek().Time >= _window)
            {
                oldest = _samples.Dequeue();
            }
            if (oldest == null)
            {
                return null;
            }
            return Quat.AngularVelocity(oldest.Value.Rotation, rotation, time - oldest.Value.Time);
        }

        public void Clear() => _samples.Clear();

        /// <summary>The roll part of an angular velocity: its component along the blade axis, either way round (°/s).</summary>
        public static float RollRate(Vec3 angularVelocity, Vec3 bladeAxis)
        {
            var n = bladeAxis.Normalized();
            return n.X == 0f && n.Y == 0f && n.Z == 0f ? 0f : Math.Abs(Vec3.Dot(angularVelocity, n));
        }
    }

    public enum TwistDecision
    {
        Disarm,
        Disabled,
        NotPlayerWeapon,
        NotEnemyWeapon,
        EnemyDeadOrPlayer,
        KeepsShield,
        InLock,
        Cooldown,
        TooSlow,
        ChanceFailed,
    }

    /// <summary>One blade contact (plain values read from the game by the caller).</summary>
    public readonly struct TwistContact
    {
        public TwistContact(
            bool enabled, bool playerWeapon, bool enemyWeapon, bool enemyDeadOrPlayer, bool keepsShield, bool inLock,
            bool coolingDown, float rollRate, float minRollRate, float roll, float chance)
        {
            Enabled = enabled;
            PlayerWeapon = playerWeapon;
            EnemyWeapon = enemyWeapon;
            EnemyDeadOrPlayer = enemyDeadOrPlayer;
            KeepsShield = keepsShield;
            InLock = inLock;
            CoolingDown = coolingDown;
            RollRate = rollRate;
            MinRollRate = minRollRate;
            Roll = roll;
            Chance = chance;
        }

        public bool Enabled { get; }

        /// <summary>The colliding item is held by the player.</summary>
        public bool PlayerWeapon { get; }

        /// <summary>The item hit is a weapon held by a creature.</summary>
        public bool EnemyWeapon { get; }

        public bool EnemyDeadOrPlayer { get; }

        /// <summary>The item hit is a shield and shields cannot be disarmed (Disarm Strike's setting).</summary>
        public bool KeepsShield { get; }

        /// <summary>A Clash of Might lock is running with that enemy: the lock decides.</summary>
        public bool InLock { get; }

        /// <summary>This enemy's blade contact already counted moments ago.</summary>
        public bool CoolingDown { get; }

        /// <summary>The fastest roll of the player's hand (°/s) just before the contact.</summary>
        public float RollRate { get; }

        public float MinRollRate { get; }

        /// <summary>0..1 dice.</summary>
        public float Roll { get; }

        /// <summary>0..1.</summary>
        public float Chance { get; }
    }

    /// <summary>
    /// Twist Disarm (spec 2b): the player's weapon hits an enemy's weapon while the blade rolls around its own long axis
    /// faster than the minimum: the enemy drops it with the chance. No stagger needed. Total angular speed does not count,
    /// as every swing turns around shoulder and elbow; only the roll around the blade does.
    /// </summary>
    public static class TwistRule
    {
        public static TwistDecision Decide(TwistContact c)
        {
            if (!c.Enabled)
            {
                return TwistDecision.Disabled;
            }
            if (!c.PlayerWeapon)
            {
                return TwistDecision.NotPlayerWeapon;
            }
            if (!c.EnemyWeapon)
            {
                return TwistDecision.NotEnemyWeapon;
            }
            if (c.EnemyDeadOrPlayer)
            {
                return TwistDecision.EnemyDeadOrPlayer;
            }
            if (c.KeepsShield)
            {
                return TwistDecision.KeepsShield;
            }
            if (c.InLock)
            {
                return TwistDecision.InLock;
            }
            if (c.CoolingDown)
            {
                return TwistDecision.Cooldown;
            }
            if (c.RollRate < c.MinRollRate)
            {
                return TwistDecision.TooSlow;
            }
            return c.Roll < c.Chance ? TwistDecision.Disarm : TwistDecision.ChanceFailed;
        }

        /// <summary>Decisions not worth a log line (no blade-on-blade contact with an enemy at all, or repeats).</summary>
        public static bool IsQuiet(TwistDecision d) =>
            d == TwistDecision.Disabled || d == TwistDecision.NotPlayerWeapon || d == TwistDecision.NotEnemyWeapon
            || d == TwistDecision.EnemyDeadOrPlayer || d == TwistDecision.Cooldown;
    }

    /// <summary>One blade contact per enemy per cooldown (a single clash raises several collision events).</summary>
    public sealed class ContactCooldown<TId>
    {
        private readonly Dictionary<TId, float> _until = new Dictionary<TId, float>();

        public bool IsCoolingDown(TId id, float now) => _until.TryGetValue(id, out var until) && now < until;

        public void Mark(TId id, float now, float seconds) => _until[id] = now + seconds;

        public void Clear() => _until.Clear();
    }
}
