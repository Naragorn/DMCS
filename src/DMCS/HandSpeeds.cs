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
