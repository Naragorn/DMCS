using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// The skill tree gate (2026-11-20): every feature has one skill in the game's Body tree, shipped as catalog JSON
/// (src/DMCS/Skills). These tests keep the code's list and the shipped files the same.
/// </summary>
public class DmcsSkillsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DMCS.sln")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string SkillsDir => Path.Combine(RepoRoot(), "src", "DMCS", "Skills");

    [Fact]
    public void EveryFeature_HasOneSkill_InTheBodyTree()
    {
        Assert.Equal(DmcsCategories.Features.Append(DmcsSkills.DoubleFatality), DmcsSkills.All.Select(s => s.Feature));
        Assert.Equal(DmcsSkills.All.Length, DmcsSkills.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal("Body", DmcsSkills.Tree);
        Assert.All(DmcsSkills.All, s => Assert.InRange(s.Tier, 0, 2));
        Assert.All(DmcsSkills.All, s => Assert.StartsWith("DMCS_", s.Id));
    }

    [Theory]
    [InlineData(DmcsCategories.ChargeStagger, "DMCS_ChargeStagger", 0)]
    [InlineData(DmcsCategories.Parry, "DMCS_Parry", 0)]
    [InlineData(DmcsCategories.DisarmStrike, "DMCS_DisarmStrike", 1)]
    [InlineData(DmcsCategories.ClashOfMight, "DMCS_ClashOfMight", 1)]
    // User 2026-11-23: Adrenaline to tier 2 of 3, Double Fatality the last tier.
    [InlineData(DmcsCategories.Adrenaline, "DMCS_Adrenaline", 1)]
    [InlineData(DmcsSkills.DoubleFatality, "DMCS_DoubleFatality", 2)]
    public void Skill_PerFeature_IdAndTier(string feature, string id, int tier)
    {
        var skill = DmcsSkills.ForFeature(feature)!;

        Assert.Equal(id, skill.Id);
        Assert.Equal(tier, skill.Tier);
        Assert.Equal(feature + " Needs Skill", skill.OptionName);
        Assert.Same(skill, DmcsSkills.ById(id));
    }

    [Fact]
    public void Lookups_UnknownOrNull_GiveNull()
    {
        Assert.Null(DmcsSkills.ForFeature("Kick"));
        Assert.Null(DmcsSkills.ForFeature(null));
        Assert.Null(DmcsSkills.ById("MightyKick"));
        Assert.Null(DmcsSkills.ById(null));
    }

    [Fact]
    public void ShippedJson_MatchesTheList()
    {
        var files = Directory.GetFiles(SkillsDir, "*.json");

        Assert.Equal(DmcsSkills.All.Length, files.Length);
        // The game's catalog skips files whose path contains "catalog_" (Catalog.ReadCatalogJsonFiles).
        Assert.All(files, f => Assert.DoesNotContain("catalog_", f, StringComparison.OrdinalIgnoreCase));
        foreach (var skill in DmcsSkills.All)
        {
            var path = Path.Combine(SkillsDir, "Skill_" + skill.Id + ".json");
            Assert.True(File.Exists(path), path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var j = doc.RootElement;

            Assert.Equal("ThunderRoad.Skill.CustomSkillData, ThunderRoad", j.GetProperty("$type").GetString());
            Assert.Equal(skill.Id, j.GetProperty("id").GetString());
            Assert.Equal(DmcsSkills.Tree, j.GetProperty("primarySkillTreeId").GetString());
            Assert.Equal(skill.Tier, j.GetProperty("tier").GetInt32());
            Assert.False(j.GetProperty("isTierBlocker").GetBoolean());
            Assert.True(j.GetProperty("showInTree").GetBoolean());
            Assert.True(j.GetProperty("allowSkill").GetBoolean());
            Assert.Equal(skill.DisplayName, j.GetProperty("skillTreeDisplayName").GetString());
            Assert.Equal(skill.Description, j.GetProperty("description").GetString());
            // Plain text: the game localizes only {braced} ids and shows the rest as written (LocalizationManager.TryGetLocalization).
            Assert.DoesNotContain("{", skill.DisplayName + skill.Description);
            // A game icon: an empty address falls back to Icons[<id>], which the game's sprite sheet does not have.
            Assert.StartsWith("Bas.Ui.SkillTree.Icons[", j.GetProperty("orbIconAddress").GetString());
            Assert.Equal(skill.Icon, j.GetProperty("orbIconAddress").GetString());
        }
    }
}
