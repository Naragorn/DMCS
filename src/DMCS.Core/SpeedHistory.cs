using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>Recent (time, speed) samples of the player; answers "fastest speed in the last N seconds".</summary>
    public sealed class SpeedHistory
    {
        private readonly Queue<(float Time, float Speed)> _samples = new Queue<(float Time, float Speed)>();
        private readonly float _retention;
        private (float Time, float Speed)? _latest;

        /// <param name="retention">Samples older than this (seconds, relative to the newest) are dropped.</param>
        public SpeedHistory(float retention)
        {
            _retention = retention;
        }

        public int Count => _samples.Count;

        public void Add(float time, float speed)
        {
            _samples.Enqueue((time, speed));
            _latest = (time, speed);
            while (_samples.Count > 0 && _samples.Peek().Time < time - _retention)
            {
                _samples.Dequeue();
            }
        }

        /// <summary>
        /// Highest speed sampled in [now - window, now]. With no sample in the window (window 0 or a
        /// frame gap), the latest sample counts: the speed the player had when the hit landed.
        /// </summary>
        public float Peak(float now, float window)
        {
            if (_latest == null)
            {
                return 0f;
            }
            var peak = _latest.Value.Speed;
            foreach (var sample in _samples)
            {
                if (sample.Time >= now - window && sample.Speed > peak)
                {
                    peak = sample.Speed;
                }
            }
            return peak;
        }

        public void Clear()
        {
            _samples.Clear();
            _latest = null;
        }
    }
}
