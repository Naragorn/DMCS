using System;

namespace DMCS.Core
{
    /// <summary>
    /// Runtime on/off state of one mechanic. The Mod Menu writes into it via <see cref="Set"/>;
    /// the mechanic subscribes to <see cref="Changed"/> to start/stop cleanly mid-fight.
    /// Skill tree gate (2026-11-20): with <see cref="NeedsSkill"/> on, the mechanic works only while the player has its
    /// skill from the game's Body tree (<see cref="HasSkill"/>, fed by DMCS's SkillGate). <see cref="Enabled"/> is what the
    /// mechanic does; <see cref="MenuEnabled"/> is what the menu shows and saves.
    /// </summary>
    public sealed class FeatureToggle
    {
        private readonly DmcsLog _log;

        public FeatureToggle(string name, bool initial, DmcsLog log = null)
        {
            Name = name;
            MenuEnabled = initial;
            _log = log;
        }

        /// <summary>Raised with the new <see cref="Enabled"/> whenever it changes, for whatever reason.</summary>
        public event Action<bool> Changed;

        public string Name { get; }

        /// <summary>The menu's own Enabled (shown and saved as is).</summary>
        public bool MenuEnabled { get; private set; }

        /// <summary>The menu's "Needs Skill" for this feature.</summary>
        public bool NeedsSkill { get; private set; }

        /// <summary>Whether the player has this feature's skill right now.</summary>
        public bool HasSkill { get; private set; }

        /// <summary>Whether the mechanic works: the menu's Enabled, and its skill when the gate is on.</summary>
        public bool Enabled => MenuEnabled && (!NeedsSkill || HasSkill);

        /// <summary>Sets the menu's Enabled.</summary>
        /// <returns>true if <see cref="Enabled"/> actually changed.</returns>
        public bool Set(bool value) => Apply(() => MenuEnabled = value);

        /// <returns>true if <see cref="Enabled"/> actually changed.</returns>
        public bool SetNeedsSkill(bool value) => Apply(() => NeedsSkill = value);

        /// <returns>true if <see cref="Enabled"/> actually changed.</returns>
        public bool SetHasSkill(bool value) => Apply(() => HasSkill = value);

        private bool Apply(Action change)
        {
            var before = Enabled;
            change();
            var now = Enabled;
            if (now == before)
            {
                return false;
            }
            _log?.Debug(Name, now ? (NeedsSkill ? "Enabled (skill bought)" : "Enabled") : (MenuEnabled ? "Disabled (needs its skill, not bought)" : "Disabled"));
            Changed?.Invoke(now);
            return true;
        }
    }
}
