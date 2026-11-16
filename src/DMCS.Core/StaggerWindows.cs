using System.Collections.Generic;

namespace DMCS.Core
{
    /// <summary>
    /// How long DMCS counts each target as staggered. The game's own stagger lasts as long as its
    /// animation; this window is DMCS's (Mod Menu "Stagger Duration"), used by Disarm Strike and to
    /// hold knocked-down NPCs on the ground.
    /// </summary>
    public sealed class StaggerWindows<TKey>
    {
        private readonly Dictionary<TKey, float> _until = new Dictionary<TKey, float>();

        public int Count => _until.Count;

        /// <summary>Staggered from <paramref name="now"/> for <paramref name="duration"/> s; never shortens a running window.</summary>
        public void Mark(TKey key, float now, float duration)
        {
            var end = now + duration;
            if (!_until.TryGetValue(key, out var current) || end > current)
            {
                _until[key] = end;
            }
        }

        public bool IsStaggered(TKey key, float now)
        {
            return _until.TryGetValue(key, out var end) && now < end;
        }

        /// <summary>Removes and returns the targets whose window has ended.</summary>
        public List<TKey> TakeExpired(float now)
        {
            var expired = new List<TKey>();
            foreach (var pair in _until)
            {
                if (now >= pair.Value)
                {
                    expired.Add(pair.Key);
                }
            }
            foreach (var key in expired)
            {
                _until.Remove(key);
            }
            return expired;
        }

        /// <summary>Forgets one target (it despawned).</summary>
        public bool Remove(TKey key)
        {
            return _until.Remove(key);
        }

        /// <summary>Removes and returns every target (feature switched off, level unloaded).</summary>
        public List<TKey> TakeAll()
        {
            var all = new List<TKey>(_until.Keys);
            _until.Clear();
            return all;
        }
    }
}
