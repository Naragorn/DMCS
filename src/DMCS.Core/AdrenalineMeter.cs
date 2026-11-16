using System;

namespace DMCS.Core
{
    public enum AdrenalineState
    {
        /// <summary>Gains add up; the meter may drain.</summary>
        Filling,

        /// <summary>Full: the window for the critical is open until <see cref="AdrenalineMeter.WindowEndsAt"/>.</summary>
        Ready,
    }

    public enum AdrenalineEvent
    {
        None,
        Gained,

        /// <summary>The meter reached its max: the window opened.</summary>
        Filled,

        /// <summary>The window ran out unused: the meter is empty.</summary>
        WindowExpired,

        /// <summary>Decay drained the meter to zero.</summary>
        Drained,
    }

    /// <summary>
    /// The Adrenaline meter: Filling → Ready (window timer) → (critical or expiry) → Filling.
    /// Times are seconds on one clock the caller picks (DMCS uses real time, so slow-mo does not stretch them).
    /// </summary>
    public sealed class AdrenalineMeter
    {
        private float _lastGainAt;

        public float Value { get; private set; }

        public AdrenalineState State { get; private set; } = AdrenalineState.Filling;

        public float WindowEndsAt { get; private set; }

        public AdrenalineEvent Gain(float amount, float now, float max, float windowSeconds)
        {
            if (amount <= 0f || State == AdrenalineState.Ready)
            {
                return AdrenalineEvent.None;
            }
            _lastGainAt = now;
            Value = Math.Min(max, Value + amount);
            if (Value < max)
            {
                return AdrenalineEvent.Gained;
            }
            State = AdrenalineState.Ready;
            WindowEndsAt = now + windowSeconds;
            return AdrenalineEvent.Filled;
        }

        /// <summary>Every frame: ends an expired window, drains a filling meter after the delay since the last gain.</summary>
        public AdrenalineEvent Tick(float now, float deltaSeconds, float decayPerSecond, float decayDelay)
        {
            if (State == AdrenalineState.Ready)
            {
                if (now < WindowEndsAt)
                {
                    return AdrenalineEvent.None;
                }
                Reset();
                return AdrenalineEvent.WindowExpired;
            }
            if (decayPerSecond <= 0f || Value <= 0f || now - _lastGainAt < decayDelay)
            {
                return AdrenalineEvent.None;
            }
            Value = Math.Max(0f, Value - (decayPerSecond * deltaSeconds));
            return Value <= 0f ? AdrenalineEvent.Drained : AdrenalineEvent.None;
        }

        /// <summary>Spends a ready meter on the critical. False if it was not ready.</summary>
        public bool Consume()
        {
            if (State != AdrenalineState.Ready)
            {
                return false;
            }
            Reset();
            return true;
        }

        /// <summary>A lowered max: a filling meter is capped (it only fills on the next gain).</summary>
        public void Cap(float max)
        {
            if (State == AdrenalineState.Filling && Value > max)
            {
                Value = max;
            }
        }

        public void Reset()
        {
            Value = 0f;
            State = AdrenalineState.Filling;
            WindowEndsAt = 0f;
        }
    }
}
