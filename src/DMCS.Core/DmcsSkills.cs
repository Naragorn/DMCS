using System.Linq;

namespace DMCS.Core
{
    /// <summary>One feature's skill in the game's skill tree.</summary>
    public sealed class DmcsSkill
    {
        public DmcsSkill(string feature, string id, int tier, string icon, string displayName, string description)
        {
            Feature = feature;
            Id = id;
            Tier = tier;
            Icon = icon;
            DisplayName = displayName;
            Description = description;
        }

        /// <summary>The feature's Mod Menu category (DmcsCategories).</summary>
        public string Feature { get; }

        /// <summary>The catalog id (SkillData.id), also the shipped file name Skill_&lt;Id&gt;.json.</summary>
        public string Id { get; }

        /// <summary>Tier in the tree, 0-based as in the game's catalog (the tree shows tier 1 for 0).</summary>
        public int Tier { get; }

        /// <summary>The orb icon: one of the game's own (a stand-in; DMCS ships no sprite).</summary>
        public string Icon { get; }

        public string DisplayName { get; }

        public string Description { get; }

        /// <summary>The Mod Menu toggle that makes the feature need this skill.</summary>
        public string OptionName => Feature + " Needs Skill";
    }

    /// <summary>
    /// The skill tree gate (user 2026-11-20, spec "Later: features as skill-tree powers"): every feature has one skill in
    /// the game's Body tree, shipped as catalog JSON (ThunderRoad.Skill.CustomSkillData, src/DMCS/Skills). A "Needs Skill"
    /// toggle per feature (default off) makes it work only once the player has bought that skill. The game adds each
    /// tier's core skill as a requirement (SkillData.OnCatalogRefresh) and prices it by tier, as its own skills.
    /// </summary>
    public static class DmcsSkills
    {
        public const string Tree = "Body";

        public const bool DefaultNeedsSkill = false;

        private const string Icons = "Bas.Ui.SkillTree.Icons[";

        /// <summary>In the spec's feature order (DmcsCategories.Features).</summary>
        public static readonly DmcsSkill[] All =
        {
            new DmcsSkill(
                DmcsCategories.ClashOfMight, "DMCS_ClashOfMight", 1, Icons + "BruteForce]", "Clash of Might",
                "When an enemy's blade flashes in a big swing, block right where it flashes: the blades lock and you push against it, or in Simple mode its sword flies away."),
            new DmcsSkill(
                DmcsCategories.DisarmStrike, "DMCS_DisarmStrike", 1, Icons + "WeaponTheft]", "Disarm Strike",
                "Hit a staggered enemy's hand, or twist your blade against its weapon, and it drops its weapon."),
            new DmcsSkill(
                DmcsCategories.ChargeStagger, "DMCS_ChargeStagger", 0, Icons + "Juggernaut]", "Charge Stagger",
                "Hit an enemy while sprinting and it staggers. A second charge hit knocks it down."),
            new DmcsSkill(
                DmcsCategories.Adrenaline, "DMCS_Adrenaline", 2, Icons + "HyperFocus]", "Adrenaline",
                "Fighting fills your adrenaline. When it is full, your next hard hit is a fatality."),
            new DmcsSkill(
                DmcsCategories.Parry, "DMCS_Parry", 0, Icons + "TemporalRiposte]", "Counter Parry",
                "Bring your weapon into an enemy's swing just in time and it staggers. Swing hard into its attack and its weapon flies away."),
        };

        public static DmcsSkill ForFeature(string feature) => feature == null ? null : All.FirstOrDefault(s => s.Feature == feature);

        public static DmcsSkill ById(string id) => id == null ? null : All.FirstOrDefault(s => s.Id == id);
    }
}
