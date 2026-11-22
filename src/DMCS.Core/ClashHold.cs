using System;

namespace DMCS.Core
{
    /// <summary>
    /// Clash Hold Blade's rules (user 2026-11-21/22): how the joint that keeps the player's blade at the lock point is set
    /// up. Hard: the blade moves freely within the slack and stops dead at its edge. Spring: past the slack a spring pulls
    /// it back (Unity's soft linear limit: a limit spring above zero makes the limit elastic, zero makes it hard).
    /// </summary>
    public static class ClashHold
    {
        public const string Spring = "Spring";
        public const string Hard = "Hard";

        public static readonly string[] Styles = { Spring, Hard };

        /// <summary>A Hard hold without slack locks the point; every other setup is a limit (hard or elastic edge).</summary>
        public static bool IsRigid(string style, float slack) => !IsSoft(style) && slack <= 0f;

        public static bool IsSoft(string style) => style == Spring;

        /// <summary>
        /// The limit spring and damper for a blade of <paramref name="mass"/> kg: it springs back at
        /// <paramref name="frequency"/> Hz with the damping ratio <paramref name="dampingRatio"/> (1 = no bounce) whatever
        /// the blade weighs (k = m·ω², d = 2·ζ·m·ω).
        /// </summary>
        public static (float Spring, float Damper) SpringFor(float mass, float frequency, float dampingRatio)
        {
            if (mass <= 0f || frequency <= 0f)
            {
                return (0f, 0f);
            }
            var omega = 2.0 * Math.PI * frequency;
            return ((float)(mass * omega * omega), (float)(2.0 * Math.Max(0f, dampingRatio) * mass * omega));
        }

        /// <summary>The whole lock, or with Hold Briefly only its first <paramref name="holdSeconds"/> (to catch the swing).</summary>
        public static bool StillHolding(float elapsed, bool briefly, float holdSeconds) => !briefly || elapsed < holdSeconds;
    }
}
