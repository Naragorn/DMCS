using System;

namespace DMCS.Core
{
    /// <summary>
    /// Runtime on/off state of one mechanic. The Mod Menu writes into it via <see cref="Set"/>;
    /// the mechanic subscribes to <see cref="Changed"/> to start/stop cleanly mid-fight.
    /// </summary>
    public sealed class FeatureToggle
    {
        private readonly DmcsLog _log;

        public FeatureToggle(string name, bool initial, DmcsLog log = null)
        {
            Name = name;
            Enabled = initial;
            _log = log;
        }

        public event Action<bool> Changed;

        public string Name { get; }

        public bool Enabled { get; private set; }

        /// <returns>true if the state actually changed.</returns>
        public bool Set(bool value)
        {
            if (value == Enabled)
            {
                return false;
            }
            Enabled = value;
            _log?.Debug(Name, value ? "Enabled" : "Disabled");
            Changed?.Invoke(value);
            return true;
        }
    }
}
