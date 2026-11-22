namespace DMCS.Core
{
    /// <summary>Mod Menu category names, order and fixed option names (single source of truth).</summary>
    public static class DmcsCategories
    {
        public const string General = "General";
        public const string ClashOfMight = "Clash of Might";
        public const string DisarmStrike = "Disarm Strike";
        public const string ChargeStagger = "Charge Stagger";
        public const string Adrenaline = "Adrenaline";
        public const string Parry = "Parry";

        /// <summary>Part of Adrenaline (its own toggle and skill gate count), a group of its own in the menu.</summary>
        public const string AdrenalineMagic = "Adrenaline Magic";

        /// <summary>Not a feature: one "Needs Skill" toggle per feature (the skill tree gate, 2026-11-20).</summary>
        public const string SkillTree = "Skill Tree";

        public const int GeneralOrder = 0;
        public const int ClashOfMightOrder = 1;
        public const int DisarmStrikeOrder = 2;
        public const int ChargeStaggerOrder = 3;
        public const int AdrenalineOrder = 4;
        public const int AdrenalineMagicOrder = 5;
        public const int ParryOrder = 6;
        public const int SkillTreeOrder = 7;

        /// <summary>
        /// The game's own "Enabled" text (Texts/Text_*.json, group "Default", id "Enabled").
        /// Each group's toggle displays as "Enabled" through this id while keeping a unique
        /// internal name, because ModOption values are saved keyed by (mod folder, option name).
        /// </summary>
        public const string EnabledLocalizationId = "Default.Enabled";

        public const string DebugLoggingName = "Debug Logging";
        public const string ResetToDefaultsName = "Reset To Defaults";

        public static readonly string[] Features = { ClashOfMight, DisarmStrike, ChargeStagger, Adrenaline, Parry };

        public static int OrderOf(string category)
        {
            switch (category)
            {
                case General: return GeneralOrder;
                case ClashOfMight: return ClashOfMightOrder;
                case DisarmStrike: return DisarmStrikeOrder;
                case ChargeStagger: return ChargeStaggerOrder;
                case Adrenaline: return AdrenalineOrder;
                case AdrenalineMagic: return AdrenalineMagicOrder;
                case Parry: return ParryOrder;
                case SkillTree: return SkillTreeOrder;
                default: return -1;
            }
        }
    }
}
