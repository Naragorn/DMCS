using System;

namespace DMCS.Core
{
    /// <summary>
    /// Mod logger. <see cref="Info"/> always writes; <see cref="Debug"/> only when the
    /// "Debug Logging" Mod Menu toggle is on. A failing sink never propagates into gameplay.
    /// </summary>
    public sealed class DmcsLog
    {
        private const string Prefix = "[DMCS]";
        private readonly Action<string> _sink;

        public DmcsLog(Action<string> sink)
        {
            _sink = sink;
        }

        public bool DebugEnabled { get; set; }

        public void Info(string message)
        {
            Write(Prefix + " " + message);
        }

        public void Debug(string channel, string message)
        {
            if (!DebugEnabled)
            {
                return;
            }
            Write(Prefix + "[" + channel + "] " + message);
        }

        private void Write(string line)
        {
            if (_sink == null)
            {
                return;
            }
            try
            {
                _sink(line);
            }
            catch (Exception)
            {
                // Logging must never break the game loop.
            }
        }
    }
}
