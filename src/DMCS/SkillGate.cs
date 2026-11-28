using System;
using System.Linq;
using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>
    /// The skill tree gate (user 2026-11-20): one "Needs Skill" toggle per feature (default off). On, the feature works
    /// only while the player has its skill from the game's Body tree (DmcsSkills; shipped as catalog JSON in Skills/).
    /// Whether the player has it is read from the game's own events: buying or refunding a skill adds or removes it in
    /// the player's container, and the game's Mana then calls Creature.ForceLoadSkill / ForceUnloadSkill, which raise
    /// SkillData.OnSkillLoadedEvent / OnSkillUnloadedEvent (read in the game code 1.3.2). At possession and level load
    /// DMCS reads the container itself (Creature.HasSkill), as the spawn's own skill loading may come before the
    /// creature is the player's.
    /// </summary>
    public static class SkillGate
    {
        private const string Channel = "Skill Tree";
        private const string Suffix = " Needs Skill";
        private const string TooltipStart = "On: works only once you have bought its skill (";
        private const string TooltipEnd = ") in the Body tree of the game's skill tree. Off: works without it.";

        private static bool _hooked;

        [ModOption(name = DmcsCategories.ClashOfMight + Suffix, tooltip = "Clash of Might. " + TooltipStart + "\"Clash of Might\", tier 2" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 0)]
        public static bool ClashOfMightNeedsSkill
        {
            get => ClashOfMight.Toggle.NeedsSkill;
            set => SetNeeds(ClashOfMight.Toggle, value);
        }

        [ModOption(name = DmcsCategories.DisarmStrike + Suffix, tooltip = "Disarm Strike. " + TooltipStart + "\"Disarm Strike\", tier 2" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 1)]
        public static bool DisarmStrikeNeedsSkill
        {
            get => DisarmStrike.Toggle.NeedsSkill;
            set => SetNeeds(DisarmStrike.Toggle, value);
        }

        [ModOption(name = DmcsCategories.ChargeStagger + Suffix, tooltip = "Charge Stagger. " + TooltipStart + "\"Charge Stagger\", tier 1" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 2)]
        public static bool ChargeStaggerNeedsSkill
        {
            get => ChargeStagger.Toggle.NeedsSkill;
            set => SetNeeds(ChargeStagger.Toggle, value);
        }

        [ModOption(name = DmcsCategories.Adrenaline + Suffix, tooltip = "Adrenaline. " + TooltipStart + "\"Adrenaline\", tier 2" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 3)]
        public static bool AdrenalineNeedsSkill
        {
            get => Adrenaline.Toggle.NeedsSkill;
            set => SetNeeds(Adrenaline.Toggle, value);
        }

        [ModOption(name = DmcsCategories.Parry + Suffix, tooltip = "Counter Parry and Power Parry. " + TooltipStart + "\"Counter Parry\", tier 1" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 4)]
        public static bool ParryNeedsSkill
        {
            get => Parry.Toggle.NeedsSkill;
            set => SetNeeds(Parry.Toggle, value);
        }

        [ModOption(name = DmcsSkills.DoubleFatality + Suffix, tooltip = "Double Fatality (Adrenaline group). " + TooltipStart + "\"Double Fatality\", tier 3" + TooltipEnd, category = DmcsCategories.SkillTree, categoryOrder = DmcsCategories.SkillTreeOrder, order = 5)]
        public static bool DoubleFatalityNeedsSkill
        {
            get => Adrenaline.DoubleFatalityToggle.NeedsSkill;
            set => SetNeeds(Adrenaline.DoubleFatalityToggle, value);
        }

        public static void Start()
        {
            if (_hooked)
            {
                return;
            }
            SkillData.OnSkillLoadedEvent += OnSkillLoaded;
            SkillData.OnSkillUnloadedEvent += OnSkillUnloaded;
            EventManager.onPossess += OnPossess;
            _hooked = true;
        }

        public static void Stop()
        {
            if (!_hooked)
            {
                return;
            }
            SkillData.OnSkillLoadedEvent -= OnSkillLoaded;
            SkillData.OnSkillUnloadedEvent -= OnSkillUnloaded;
            EventManager.onPossess -= OnPossess;
            _hooked = false;
        }

        /// <summary>At level load: reads the player's skills and logs whether the game's catalog has DMCS's skills.</summary>
        public static void OnLevelLoaded()
        {
            ReadPlayer(CurrentPlayer.Body, "level loaded");
            Dmcs.Log.Info("Skill tree: " + string.Join(", ", DmcsSkills.All.Select(Describe)));
        }

        private static string Describe(DmcsSkill skill)
        {
            if (!Catalog.TryGetData<SkillData>(skill.Id, out var data, false) || data == null)
            {
                return skill.Id + " NOT in the game's catalog (Skills/" + "Skill_" + skill.Id + ".json missing?)";
            }
            var owned = Toggle(skill)?.HasSkill == true ? ", bought" : "";
            return skill.Id + " in " + data.primarySkillTreeId + " tier " + (data.tier + 1) + " (cost " + data.Cost + owned + ")";
        }

        private static FeatureToggle Toggle(DmcsSkill skill) =>
            Features.All.Append(Adrenaline.DoubleFatalityToggle).FirstOrDefault(t => t.Name == skill.Feature);

        private static void SetNeeds(FeatureToggle toggle, bool value)
        {
            if (value && !toggle.NeedsSkill)
            {
                // The player may have bought the skill before the gate was switched on: read it now.
                var skill = DmcsSkills.ForFeature(toggle.Name);
                var player = CurrentPlayer.Body;
                if (skill != null && player != null)
                {
                    toggle.SetHasSkill(player.HasSkill(skill.Id));
                }
            }
            toggle.SetNeedsSkill(value);
        }

        private static void ReadPlayer(Creature player, string why)
        {
            if (player == null)
            {
                return;
            }
            foreach (var skill in DmcsSkills.All)
            {
                var toggle = Toggle(skill);
                if (toggle != null && toggle.SetHasSkill(player.HasSkill(skill.Id)))
                {
                    Dmcs.Log.Debug(Channel, skill.DisplayName + ": " + (toggle.HasSkill ? "bought" : "not bought") + " (" + why + ")");
                }
            }
        }

        private static void OnPossess(Creature creature, EventTime eventTime)
        {
            try
            {
                if (eventTime == EventTime.OnEnd && creature != null && creature.isPlayer)
                {
                    ReadPlayer(creature, "possessed");
                }
            }
            catch (Exception e)
            {
                Dmcs.Log.Info("Skill Tree error: " + e);
            }
        }

        private static void OnSkillLoaded(SkillData data, Creature creature) => OnSkill(data, creature, true);

        private static void OnSkillUnloaded(SkillData data, Creature creature) => OnSkill(data, creature, false);

        private static void OnSkill(SkillData data, Creature creature, bool loaded)
        {
            try
            {
                var skill = DmcsSkills.ById(data?.id);
                if (skill == null || creature == null || !creature.isPlayer)
                {
                    return;
                }
                var toggle = Toggle(skill);
                if (toggle == null)
                {
                    return;
                }
                toggle.SetHasSkill(loaded);
                Dmcs.Log.Debug(Channel, skill.DisplayName + (loaded ? ": skill loaded" : ": skill unloaded") + (toggle.NeedsSkill ? ", " + toggle.Name + (toggle.Enabled ? " works" : " is off") : " (not needed: " + toggle.Name + " works without it)"));
            }
            catch (Exception e)
            {
                Dmcs.Log.Info("Skill Tree error: " + e);
            }
        }
    }
}
