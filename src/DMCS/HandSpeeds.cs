using System.Collections.Generic;
using DMCS.Core;
using ThunderRoad;
using UnityEngine;

namespace DMCS
{
    /// <summary>
    /// The speed of the player's real hands: each controller's tracked pose (PlayerHand, driven by its TrackedPoseDriver),
    /// local to the rig so walking and turning do not count, measured over 0.05 s (frames come faster than the tracking).
    /// </summary>
    internal static class HandSpeeds
    {
        private const float MeterWindow = 0.05f;
        private const float Retention = 1f;

        private static readonly HeadSpeedMeter LeftMeter = new HeadSpeedMeter(MeterWindow);
        private static readonly HeadSpeedMeter RightMeter = new HeadSpeedMeter(MeterWindow);
        private static readonly SpeedHistory LeftHistory = new SpeedHistory(Retention);
        private static readonly SpeedHistory RightHistory = new SpeedHistory(Retention);
        private static readonly RollMeter LeftRoll = new RollMeter(TwistSettings.MeterWindow);
        private static readonly RollMeter RightRoll = new RollMeter(TwistSettings.MeterWindow);
        private static readonly Queue<(float Time, Vec3 Spin)> LeftSpins = new Queue<(float Time, Vec3 Spin)>();
        private static readonly Queue<(float Time, Vec3 Spin)> RightSpins = new Queue<(float Time, Vec3 Spin)>();

        public static void Update()
        {
            var player = Player.local;
            if (player == null)
            {
                return;
            }
            var now = Time.unscaledTime;
            Sample(player.handLeft, LeftMeter, LeftHistory, now);
            Sample(player.handRight, RightMeter, RightHistory, now);
            SampleSpin(player.handLeft, LeftRoll, LeftSpins, now);
            SampleSpin(player.handRight, RightRoll, RightSpins, now);
        }

        /// <summary>
        /// Fastest roll (°/s) of that hand's controller around a world axis (the held blade's long axis) in the last
        /// <paramref name="window"/> seconds. The controller's spin is kept rig-local; the axis is turned into the rig too.
        /// </summary>
        public static float PeakRoll(Side side, Vector3 worldAxis, float window)
        {
            var player = Player.local;
            var hand = player == null ? null : side == Side.Left ? player.handLeft : player.handRight;
            if (hand == null)
            {
                return 0f;
            }
            var rig = hand.transform.parent;
            var axis = rig != null ? rig.InverseTransformDirection(worldAxis) : worldAxis;
            var a = new Vec3(axis.x, axis.y, axis.z);
            var now = Time.unscaledTime;
            var peak = 0f;
            foreach (var (time, spin) in side == Side.Left ? LeftSpins : RightSpins)
            {
                if (time >= now - window)
                {
                    peak = Mathf.Max(peak, RollMeter.RollRate(spin, a));
                }
            }
            return peak;
        }

        /// <summary>Fastest speed (m/s) of that hand's controller in the last <paramref name="window"/> seconds.</summary>
        public static float Peak(Side side, float window)
        {
            return (side == Side.Left ? LeftHistory : RightHistory).Peak(Time.unscaledTime, window);
        }

        public static void Clear()
        {
            LeftMeter.Clear();
            RightMeter.Clear();
            LeftHistory.Clear();
            RightHistory.Clear();
            LeftRoll.Clear();
            RightRoll.Clear();
            LeftSpins.Clear();
            RightSpins.Clear();
        }

        private static void SampleSpin(PlayerHand hand, RollMeter meter, Queue<(float Time, Vec3 Spin)> spins, float now)
        {
            if (hand == null)
            {
                return;
            }
            var q = hand.transform.localRotation;
            var spin = meter.Add(now, new Quat(q.x, q.y, q.z, q.w));
            if (spin.HasValue)
            {
                spins.Enqueue((now, spin.Value));
            }
            while (spins.Count > 0 && spins.Peek().Time < now - Retention)
            {
                spins.Dequeue();
            }
        }

        private static void Sample(PlayerHand hand, HeadSpeedMeter meter, SpeedHistory history, float now)
        {
            if (hand == null)
            {
                return;
            }
            var p = hand.transform.localPosition;
            var speed = meter.Add(now, p.x, p.y, p.z);
            if (speed.HasValue)
            {
                history.Add(now, speed.Value);
            }
        }
    }
}
