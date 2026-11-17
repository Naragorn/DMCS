using System;

namespace DMCS.Core
{
    /// <summary>When the adrenaline signals run: the feature is on and the adrenaline window or an execution window is open.</summary>
    public static class AdrenalineSignalRule
    {
        public static bool Active(bool featureEnabled, bool adrenalineWindowOpen, bool executionWindowOpen) =>
            featureEnabled && (adrenalineWindowOpen || executionWindowOpen);
    }

    /// <summary>A level 0..1 that follows on/off with linear fades (seconds; 0 = at once).</summary>
    public sealed class SignalFader
    {
        private readonly float _fadeIn;
        private readonly float _fadeOut;

        public SignalFader(float fadeIn, float fadeOut)
        {
            _fadeIn = fadeIn;
            _fadeOut = fadeOut;
        }

        public float Level { get; private set; }

        public float Update(bool on, float dt)
        {
            if (on)
            {
                Level = _fadeIn <= 0f ? 1f : Math.Min(1f, Level + (dt / _fadeIn));
            }
            else
            {
                Level = _fadeOut <= 0f ? 0f : Math.Max(0f, Level - (dt / _fadeOut));
            }
            return Level;
        }

        public void Reset() => Level = 0f;
    }

    /// <summary>A one-shot flare: 1 at the trigger, easing out to 0 over its duration.</summary>
    public sealed class SignalFlare
    {
        private readonly float _duration;
        private float _start = float.NegativeInfinity;

        public SignalFlare(float duration)
        {
            _duration = duration;
        }

        public void Trigger(float now) => _start = now;

        public void Reset() => _start = float.NegativeInfinity;

        public float Level(float now)
        {
            var t = now - _start;
            if (t < 0f || t >= _duration || _duration <= 0f)
            {
                return 0f;
            }
            var u = 1f - (t / _duration);
            return u * u;
        }
    }

    /// <summary>A "lub-dub" haptic heartbeat: a strong beat, a weaker one <see cref="DubDelay"/> later, then rest.</summary>
    public static class HeartbeatPulse
    {
        public const float DubDelay = 0.28f;
        public const float BeatLength = 0.09f;
        public const float DubLevel = 0.55f;

        public static float At(float seconds, float bpm)
        {
            if (bpm <= 0f)
            {
                return 0f;
            }
            var period = 60f / bpm;
            var t = seconds % period;
            if (t < 0f)
            {
                t += period;
            }
            return Math.Max(Beat(t), DubLevel * Beat(t - DubDelay));
        }

        private static float Beat(float t) => t < 0f || t >= BeatLength ? 0f : (float)Math.Sin(Math.PI * t / BeatLength);
    }
}
