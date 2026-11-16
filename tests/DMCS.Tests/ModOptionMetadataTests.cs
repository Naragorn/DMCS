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
    public void ChargeStagger_ExposesItsSettings_InOrder_WithSlidersForNumbersAndDropdownsForChoices()
    {
        var group = Options.Value.Where(o => o.Category == DmcsCategories.ChargeStagger).OrderBy(o => o.Order).ToList();

        Assert.Equal(
            new[]
            {
                (DmcsCategories.ChargeStagger + " Enabled", "System.Boolean", "ArrowList"),
                (ChargeStaggerSettings.PresetName, "System.String", "ArrowList"),
                (ChargeStaggerSettings.ChanceName, "System.Int32", "Slider"),
                (ChargeStaggerSettings.SpeedThresholdName, "System.Single", "Slider"),
                (ChargeStaggerSettings.SpeedWindowName, "System.Single", "Slider"),
                (ChargeStaggerSettings.MinSwingSpeedName, "System.Single", "Slider"),
                (ChargeStaggerSettings.RangedCountsName, "System.Boolean", "ArrowList"),
                (ChargeStaggerSettings.StrengthName, "System.String", "ArrowList"),
                (ChargeStaggerSettings.DurationName, "System.Single", "Slider"),
                (ChargeStaggerSettings.SecondKnocksDownName, "System.Boolean", "ArrowList"),
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
                (DisarmStrikeSettings.NoRegrabTimeName, "System.Single", "Slider"),
                (DisarmStrikeSettings.StayDisarmedName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.StandStillName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.BodyHitDropName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.BodyHitDropChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.DropChancePerExtraHitName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.LoosenedSparkName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.LoosenedHapticsName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.ShieldsName, "System.Boolean", "ArrowList"),
                (DisarmStrikeSettings.FlingForceName, "System.Single", "Slider"),
                (DisarmStrikeSettings.FemaleChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.SmallerChanceName, "System.Int32", "Slider"),
                (DisarmStrikeSettings.SmallerMarginName, "System.Single", "Slider"),
            },
            group.Select(o => (o.Name, o.DataType, o.InteractionType)));
        Assert.All(group, o => Assert.True(o.SaveValue, o.Name + " must be saved"));
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
    public void Categories_AreExactlyGeneralPlusTheFourFeatures()
    {
        var categories = Options.Value.Select(o => o.Category).Distinct().OrderBy(c => c).ToArray();
        var expected = new[] { DmcsCategories.General }.Concat(DmcsCategories.Features).OrderBy(c => c).ToArray();

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

    [Theory]
    [InlineData(DmcsCategories.ClashOfMight)]
    [InlineData(DmcsCategories.DisarmStrike)]
    [InlineData(DmcsCategories.ChargeStagger)]
    [InlineData(DmcsCategories.Adrenaline)]
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
