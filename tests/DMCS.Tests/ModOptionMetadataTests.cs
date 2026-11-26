using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// Inspects the built DMCS.dll the same way the game's ModManager discovers options
/// (static members carrying ThunderRoad.ModOption + ModOptionAttribute subclasses),
/// without executing any game code. Guards the Mod Menu contract from CLAUDE.md.
/// </summary>
public class ModOptionMetadataTests
{
    private static readonly Lazy<List<OptionInfo>> Options = new(LoadOptions);

    private sealed class OptionInfo
    {
        public string Member = "";
        public string DeclaringType = "";
        public bool IsStatic;
        public string DataType = "";
        public string Name = "";
        public string NameLocalizationId;
        public string Tooltip;
        public string Category;
        public int CategoryOrder = int.MaxValue;
        public int Order = int.MaxValue;
        public bool SaveValue = true;
        public bool ApplyOnLoad = true;
        public string InteractionType = "ArrowList";
        public string ValueSource;
    }

    private static string Meta(string key) =>
        typeof(ModOptionMetadataTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == key).Value!;

    private static List<OptionInfo> LoadOptions()
    {
        var managed = Meta("BasManagedDir");
        var modDll = Path.GetFullPath(Meta("ModDllPath"));
        Assert.True(Directory.Exists(managed), $"Blade & Sorcery Managed dir not found: {managed}. Set BasGameDir.");
        Assert.True(File.Exists(modDll), $"Mod DLL not built: {modDll}");

        var paths = Directory.GetFiles(managed, "*.dll").Append(modDll);
        var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
        var asm = mlc.LoadFromAssemblyPath(modDll);

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var result = new List<OptionInfo>();
        foreach (var type in asm.GetTypes())
        {
            foreach (var member in type.GetMembers(all))
            {
                var attrs = member.GetCustomAttributesData();
                var main = attrs.FirstOrDefault(a => a.AttributeType.FullName == "ThunderRoad.ModOption");
                if (main == null)
                {
                    continue;
                }
                result.Add(Describe(type, member, main, attrs));
            }
        }
        return result;
    }

    private static OptionInfo Describe(Type type, MemberInfo member, CustomAttributeData main, IList<CustomAttributeData> attrs)
    {
        var info = new OptionInfo { Member = member.Name, DeclaringType = type.FullName! };
        switch (member)
        {
            case FieldInfo f:
                info.IsStatic = f.IsStatic;
                info.DataType = f.FieldType.FullName!;
                break;
            case PropertyInfo p:
                info.IsStatic = p.GetGetMethod(true)?.IsStatic ?? p.GetSetMethod(true)!.IsStatic;
                info.DataType = p.PropertyType.FullName!;
                break;
            case MethodInfo m:
                info.IsStatic = m.IsStatic;
                info.DataType = m.GetParameters().Single().ParameterType.FullName!;
                break;
        }

        var ctorArgs = main.ConstructorArguments;
        if (ctorArgs.Count > 0) info.Name = (string)ctorArgs[0].Value!;
        if (ctorArgs.Count > 1) info.Tooltip = (string)ctorArgs[1].Value;
        foreach (var arg in main.NamedArguments)
        {
            var v = arg.TypedValue.Value;
            switch (arg.MemberName)
            {
                case "name": info.Name = (string)v!; break;
                case "nameLocalizationId": info.NameLocalizationId = (string)v; break;
                case "tooltip": info.Tooltip = (string)v; break;
                case "category": info.Category = (string)v; break;
                case "categoryOrder": info.CategoryOrder = (int)v!; break;
                case "order": info.Order = (int)v!; break;
                case "saveValue": info.SaveValue = (bool)v!; break;
                case "applyOnLoad": info.ApplyOnLoad = (bool)v!; break;
                case "valueSourceName": info.ValueSource = (string)v; break;
                case "interactionType": info.InteractionType = ((int)v!) switch { 0 => "ArrowList", 1 => "ButtonList", 2 => "Slider", 3 => "Text", _ => "?" }; break;
            }
        }
        // Marker attributes, processed the way ThunderRoad's ModOptionAttribute.Process() does.
        foreach (var a in attrs)
        {
            switch (a.AttributeType.FullName)
            {
                case "ThunderRoad.ModOptionButton": info.InteractionType = "ButtonList"; break;
                case "ThunderRoad.ModOptionSlider": info.InteractionType = "Slider"; break;
                case "ThunderRoad.ModOptionArrows": info.InteractionType = "ArrowList"; break;
                case "ThunderRoad.ModOptionDontSave": info.SaveValue = false; break;
                case "ThunderRoad.ModOptionApplyOnPress": info.ApplyOnLoad = false; break;
                case "ThunderRoad.ModOptionTooltip": info.Tooltip = (string)a.ConstructorArguments[0].Value; break;
                case "ThunderRoad.ModOptionOrder": info.Order = (int)a.ConstructorArguments[0].Value!; break;
                case "ThunderRoad.ModOptionCategory":
                    info.Category = (string)a.ConstructorArguments[0].Value;
                    info.CategoryOrder = (int)a.ConstructorArguments[1].Value!;
                    break;
            }
        }
        return info;
    }

    [Fact]
    public void Advanced_HoldsTheFineTuning_InTheOrderOfTheFeatureGroups()
    {
        // User 2026-11-26: fine-tuning values move to their own tab (category) at the end, so the feature tabs stay short.
        // Names stay the same: the game saves values keyed by (mod folder, option name), so saved values carry over.
        var group = Options.Value.Where(o => o.Category == DmcsCategories.Advanced).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (PlayerStrikeRule.MinHandSpeedName, "System.Single", "Slider"),
                (ClashSettings.NpcReachName, "System.Single", "Slider"),
                (ClashSettings.WeaponWeightName, "System.Single", "Slider"),
                (ClashSettings.HoldStyleName, "System.String", "ArrowList"),
                (ClashSettings.HoldSlackName, "System.Single", "Slider"),
                (ClashSettings.HoldSpringName, "System.Single", "Slider"),
                (ClashSettings.HoldSpringDampingName, "System.Single", "Slider"),
                (ClashSettings.HoldRotationName, "System.Boolean", "ArrowList"),
                (ClashSettings.HoldBrieflyName, "System.Boolean", "ArrowList"),
                (ClashSettings.HoldTimeName, "System.Single", "Slider"),
                (ClashSettings.FlashAtTopName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.EnemyPushName, "System.Single", "Slider"),
                (ClashFightSettings.DeadZoneName, "System.Single", "Slider"),
                (ClashFightSettings.PushPhaseName, "System.Single", "Slider"),
                (DisarmStrikeSettings.ShockTimeName, "System.Single", "Slider"),
                (DisarmStrikeSettings.BackOffTimeName, "System.Single", "Slider"),
                (DisarmStrikeSettings.CowerTimeName, "System.Single", "Slider"),
                (DisarmStrikeSettings.FlingForceName, "System.Single", "Slider"),
                (DisarmStrikeSettings.FemaleChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.SmallerChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.SmallerMarginName, "System.Single", "Slider"),
                (ChargeStaggerSettings.ChanceName, "System.Int32", "Slider"),
                (ChargeStaggerSettings.SpeedThresholdName, "System.Single", "Slider"),
                (ChargeStaggerSettings.SpeedWindowName, "System.Single", "Slider"),
                (ChargeStaggerSettings.MinSwingSpeedName, "System.Single", "Slider"),
                (ChargeStaggerSettings.StrengthName, "System.String", "ArrowList"),
                (ChargeStaggerSettings.DurationName, "System.Single", "Slider"),
                (AdrenalineSettings.MeterMaxName, "System.Single", "Slider"),
                (AdrenalineSettings.GainHitName, "System.Int32", "Slider"),
                (AdrenalineSettings.GainBlockParryName, "System.Int32", "Slider"),
                (AdrenalineSettings.GainDamageTakenName, "System.Int32", "Slider"),
                (AdrenalineSettings.GainChargeStaggerName, "System.Int32", "Slider"),
                (AdrenalineSettings.GainDisarmName, "System.Int32", "Slider"),
                (AdrenalineSettings.GainDodgeName, "System.Int32", "Slider"),
                (AdrenalineSettings.DodgeMinSpeedName, "System.Single", "Slider"),
                (AdrenalineSettings.GainCatchName, "System.Int32", "Slider"),
                (AdrenalineSettings.CatchMaxAirTimeName, "System.Single", "Slider"),
                (AdrenalineSettings.GainClashWinName, "System.Int32", "Slider"),
                (AdrenalineSettings.HyperfocusScaleName, "System.Single", "Slider"),
                (AdrenalineSettings.SlowTimeScaleName, "System.Single", "Slider"),
                (AdrenalineSettings.KnockbackName, "System.Single", "Slider"),
                (AdrenalineSettings.ExecutionWindowName, "System.Single", "Slider"),
                (AdrenalineSettings.KneelAfterNoHitName, "System.Single", "Slider"),
                (AdrenalineSettings.KneelAfterLightHitsName, "System.Int32", "Slider"),
                (AdrenalineSettings.KneelMaxDistance.Name, "System.Single", "Slider"),
                (AdrenalineSettings.VignetteHueName, "System.Single", "Slider"),
                (AdrenalineSettings.VignetteStreaksName, "System.Int32", "Slider"),
                (AdrenalineSettings.VignetteLengthName, "System.Single", "Slider"),
                (AdrenalineSettings.VignetteFrameRateName, "System.Single", "Slider"),
                (AdrenalineMagicSettings.FireRadius.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.FireSpreadHeat.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.LightningRadius.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.LightningShock.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.GravityRadius.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.GravityForce.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.TelekinesisThrow.Name, "System.Single", "Slider"),
                (AdrenalineMagicSettings.TelekinesisImpactTime.Name, "System.Single", "Slider"),
                (ParrySettings.StaggerDurationName, "System.Single", "Slider"),
                (ParrySettings.SpeedWindowName, "System.Single", "Slider"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
        Assert.Equal(59, group.Count);
    }

    [Fact]
    public void Advanced_IsTheLastTab()
    {
        Assert.True(Options.Value.Where(o => o.Category != DmcsCategories.Advanced).All(o => o.CategoryOrder < DmcsCategories.AdvancedOrder));
    }

    [Fact]
    public void ChargeStagger_ExposesItsSettings_InOrder_WithSlidersForNumbersAndDropdownsForChoices()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.ChargeStagger).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (DmcsCategories.ChargeStagger + " Enabled", "System.Boolean", "ArrowList"),
                (ChargeStaggerSettings.PresetName, "System.String", "ArrowList"),
                (ChargeStaggerSettings.RangedCountsName, "System.Boolean", "ArrowList"),
                (ChargeStaggerSettings.SecondKnocksDownName, "System.Boolean", "ArrowList"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
    }

    [Fact]
    public void ClashOfMight_ExposesItsSettings_InOrder()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.ClashOfMight).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (ClashModes.ModeName, "System.String", "ArrowList"),
                (ClashSettings.OnlySwordsName, "System.Boolean", "ArrowList"),
                (ClashSettings.MinWindUpName, "System.Single", "Slider"),
                (ClashSettings.SlowMoName, "System.Boolean", "ArrowList"),
                (ClashSettings.SlowMoScaleName, "System.Single", "Slider"),
                (ClashSettings.HoldBladeName, "System.Boolean", "ArrowList"),
                (ClashSettings.FlashSoundName, "System.String", "ArrowList"),
                (ClashSettings.FlashGameSoundName, "System.String", "ArrowList"),
                (ClashSettings.FlashSoundVolumeName, "System.Single", "Slider"),
                (ClashFightSettings.DifficultyName, "System.Single", "Slider"),
                (ClashFightSettings.ScalesWithHeightName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.ScalesWithGenderName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.FemaleStrengthName, "System.Single", "Slider"),
                (ClashFightSettings.MinRoundsName, "System.Single", "Slider"),
                (ClashFightSettings.MaxRoundsName, "System.Single", "Slider"),
                (ClashFightSettings.FewerRoundsName, "System.Single", "Slider"),
                (ClashFightSettings.SidePushName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.WinDisarmsName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.WinKneelChanceName, "System.Single", "Slider"),
                (ClashFightSettings.LoseOutcomeName, "System.String", "ArrowList"),
                (ClashFightSettings.LoseDamageName, "System.Single", "Slider"),
                (ClashFightSettings.HapticsName, "System.Single", "Slider"),
                (ClashFightSettings.DuelStarName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.OutcomeCuesName, "System.Boolean", "ArrowList"),
                (ClashFightSettings.DarkMessiahSoundsName, "System.Boolean", "ArrowList"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
    }

    [Fact]
    public void DisarmStrike_ExposesItsSettings_InOrder()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.DisarmStrike).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (DmcsCategories.DisarmStrike + " Enabled", "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.RequireStaggerName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.IncludeForearmName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.DropBothHandsName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.ChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.ShockChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.BackOffChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.CowerChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.StayDisarmedName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.BodyHitDropName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.BodyHitDropChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.DropChancePerExtraHitName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.LoosenedSparkName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.LoosenedHapticsName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.ShieldsName, "System.Boolean", "ArrowList"),
                (TwistSettings.EnabledName, "System.Boolean", "ArrowList"),
                (TwistSettings.ChanceName, "System.Single", "Slider"),
                (TwistSettings.MinRollSpeedName, "System.Single", "Slider"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
    }

    [Fact]
    public void Parry_ExposesItsSettings_InOrder()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.Parry).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (ParrySettings.EnabledName, "System.Boolean", "ArrowList"),
                (ParrySettings.CounterName, "System.Boolean", "ArrowList"),
                (ParrySettings.CounterSpeedName, "System.Single", "Slider"),
                (ParrySettings.PowerName, "System.Boolean", "ArrowList"),
                (ParrySettings.PowerSpeedName, "System.Single", "Slider"),
                (ParrySettings.PowerChanceName, "System.Single", "Slider"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
    }

    [Fact]
    public void Adrenaline_ExposesItsSettings_InOrder()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.Adrenaline).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (DmcsCategories.Adrenaline + " Enabled", "System.Boolean", "ArrowList"),
                (AdrenalineSettings.WindowDurationName, "System.Single", "Slider"),
                (AdrenalineSettings.DecayEnabledName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.DecayPerSecondName, "System.Single", "Slider"),
                (AdrenalineSettings.DecayDelayName, "System.Single", "Slider"),
                (AdrenalineSettings.DamageTakenLowersName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.CriticalTimeModeName, "System.String", "ArrowList"),
                (AdrenalineSettings.HyperfocusDurationName, "System.Single", "Slider"),
                (AdrenalineSettings.SlowTimeDurationName, "System.Single", "Slider"),
                (AdrenalineSettings.NeedsHardSwingName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.MinSwingSpeedName, "System.Single", "Slider"),
                (AdrenalineSettings.RangedCountsName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.KillsName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.DismemberName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.BattleCryName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.ChestStabKneelsName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.KneelEnabledName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.KneelDeathChanceName, "System.Int32", "Slider"),
                (AdrenalineSettings.ReadySoundName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.ReadySoundStyleName, "System.String", "ArrowList"),
                (AdrenalineSettings.ReadySoundVolumeName, "System.Single", "Slider"),
                (AdrenalineSettings.HeartbeatName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.HeartbeatSoundName, "System.String", "ArrowList"),
                (AdrenalineSettings.HeartbeatVolumeName, "System.Single", "Slider"),
                (AdrenalineSettings.HapticsName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.HapticsStrengthName, "System.Single", "Slider"),
                (AdrenalineSettings.WeaponGlowName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.VignetteName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.VignetteStyleName, "System.String", "ArrowList"),
                (AdrenalineSettings.VignetteIntensityName, "System.Single", "Slider"),
                (AdrenalineSettings.VignetteSizeName, "System.Single", "Slider"),
                (AdrenalineSettings.VignetteFlareName, "System.Boolean", "ArrowList"),
                (AdrenalineSettings.DoubleFatalityName, "System.Boolean", "ArrowList"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
    }

    [Fact]
    public void ChargeStaggerPreset_IsNotAppliedOnLoad_SoItNeverOverwritesTheSavedValues()
    {
        var preset = Assert.Single(Options.Value, o => o.Name == ChargeStaggerSettings.PresetName);

        Assert.False(preset.ApplyOnLoad);
    }

    [Fact]
    public void ModDll_ExposesModOptions()
    {
        Assert.NotEmpty(Options.Value);
    }

    [Fact]
    public void Categories_AreExactlyGeneral_TheFeatures_AdrenalineMagic_SkillTree_AndAdvanced()
    {
        var categories = Options.Value.Select(o => o.Category).Distinct().OrderBy(c => c).ToArray();
        var expected = new[] { DmcsCategories.General, DmcsCategories.SkillTree, DmcsCategories.AdrenalineMagic, DmcsCategories.Finisher, DmcsCategories.Advanced }.Concat(DmcsCategories.Features).OrderBy(c => c).ToArray();

        Assert.Equal(expected, categories);
    }

    [Fact]
    public void CategoryOrder_IsConsistentPerCategory_AndFollowsTheSpec()
    {
        foreach (var group in Options.Value.GroupBy(o => o.Category))
        {
            var orders = group.Select(o => o.CategoryOrder).Distinct().ToArray();
            Assert.Single(orders);
            Assert.Equal(DmcsCategories.OrderOf(group.Key), orders[0]);
        }
    }

    [Fact]
    public void ClashOfMight_StartsWithItsModeDropdown_OffSimpleOn_InsteadOfAToggle()
    {
        // User 2026-11-18: the feature is Off / Simple / On (default Simple), not a plain on/off toggle.
        var inGroup = Options.Value.Where(o => o.Category == DmcsCategories.ClashOfMight).ToList();
        var first = inGroup.OrderBy(o => o.Order).First();

        Assert.Equal(ClashModes.ModeName, first.Name);
        Assert.Equal("System.String", first.DataType);
        Assert.True(first.SaveValue);
        Assert.DoesNotContain(inGroup, o => o.NameLocalizationId == DmcsCategories.EnabledLocalizationId);
    }

    [Theory]
    [InlineData(DmcsCategories.DisarmStrike)]
    [InlineData(DmcsCategories.ChargeStagger)]
    [InlineData(DmcsCategories.Adrenaline)]
    [InlineData(DmcsCategories.Parry)]
    [InlineData(DmcsCategories.AdrenalineMagic)]
    [InlineData(DmcsCategories.Finisher)]
    public void EveryFeatureGroup_StartsWithItsEnabledToggle(string category)
    {
        var inGroup = Options.Value.Where(o => o.Category == category).ToList();
        var enabled = inGroup.Where(o => o.NameLocalizationId == DmcsCategories.EnabledLocalizationId).ToList();

        var toggle = Assert.Single(enabled);
        Assert.Equal("System.Boolean", toggle.DataType);
        Assert.Equal(inGroup.Min(o => o.Order), toggle.Order);
        Assert.Equal("ArrowList", toggle.InteractionType);
        Assert.True(toggle.SaveValue);
    }

    [Fact]
    public void AdrenalineMagic_ExposesItsSettings_InOrder()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.AdrenalineMagic).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (AdrenalineMagicSettings.EnabledName, "System.Boolean", "ArrowList"),
                (AdrenalineMagicSettings.FireName, "System.Boolean", "ArrowList"),
                (AdrenalineMagicSettings.LightningName, "System.Boolean", "ArrowList"),
                (AdrenalineMagicSettings.GravityName, "System.Boolean", "ArrowList"),
                (AdrenalineMagicSettings.TelekinesisName, "System.Boolean", "ArrowList"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group.Where(o => o.DataType != "System.Boolean"), o => Assert.False(string.IsNullOrEmpty(o.ValueSource), o.Name + " needs a value list"));
        Assert.Equal(DmcsCategories.EnabledLocalizationId, group[0].NameLocalizationId);
    }

    [Fact]
    public void SkillTree_HasOneNeedsSkillToggle_PerFeature_DefaultOff()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.SkillTree).OrderBy(o => o.Order).ToList();

        Assert.Equal(DmcsSkills.All.Select(s => s.OptionName), group.Select(o => o.Name));
        Assert.All(group, o => Assert.Equal("System.Boolean", o.DataType));
        Assert.All(group, o => Assert.Equal(DmcsCategories.SkillTreeOrder, o.CategoryOrder));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
        Assert.All(group, o => Assert.Contains("Body tree", o.Tooltip));
        Assert.False(DmcsSkills.DefaultNeedsSkill);
    }

    [Fact]
    public void OptionNames_AreUnique_BecauseTheGameSavesValuesKeyedByModFolderAndName()
    {
        var duplicates = Options.Value.GroupBy(o => o.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void EveryOption_IsStatic_HasAName_AndATooltip()
    {
        foreach (var o in Options.Value)
        {
            Assert.True(o.IsStatic, $"{o.DeclaringType}.{o.Member} must be static or the game ignores it");
            Assert.False(string.IsNullOrWhiteSpace(o.Name), $"{o.Member} has no name");
            Assert.False(string.IsNullOrWhiteSpace(o.Tooltip), $"{o.Name} has no tooltip");
        }
    }

    [Fact]
    public void General_HasDebugLoggingToggle()
    {
        var debug = Assert.Single(Options.Value, o => o.Name == DmcsCategories.DebugLoggingName);

        Assert.Equal(DmcsCategories.General, debug.Category);
        Assert.Equal("System.Boolean", debug.DataType);
        Assert.True(debug.SaveValue);
    }

    [Fact]
    public void General_HasResetButton_ThatIsNeitherSavedNorFiredOnLoad()
    {
        var reset = Assert.Single(Options.Value, o => o.Name == DmcsCategories.ResetToDefaultsName);

        Assert.Equal(DmcsCategories.General, reset.Category);
        Assert.Equal("ButtonList", reset.InteractionType);
        Assert.False(reset.SaveValue);
        Assert.False(reset.ApplyOnLoad);
        // UIButtonListModOption feeds nameLocalizationId into the tooltip (game bug), so it must stay unset.
        Assert.Null(reset.NameLocalizationId);
    }
}
