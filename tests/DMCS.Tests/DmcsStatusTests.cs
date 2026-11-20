using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class DmcsStatusTests
{
    [Fact]
    public void Describe_ListsDebugLoggingAndEveryFeature_InOrder()
    {
        var toggles = new[]
        {
            new FeatureToggle("Clash of Might", initial: true),
            new FeatureToggle("Disarm Strike", initial: false),
        };

        Assert.Equal(
            "Settings: Debug Logging=on | Clash of Might=Enabled | Disarm Strike=Disabled",
            DmcsStatus.Describe(debugLogging: true, toggles));
    }

    [Fact]
    public void Describe_WithoutFeatures()
    {
        Assert.Equal("Settings: Debug Logging=off", DmcsStatus.Describe(debugLogging: false, new FeatureToggle[0]));
    }

    [Fact]
    public void Describe_SkipsNullEntries()
    {
        Assert.Equal("Settings: Debug Logging=off", DmcsStatus.Describe(false, new FeatureToggle[] { null }));
    }

    [Fact]
    public void Describe_NullList()
    {
        Assert.Equal("Settings: Debug Logging=off", DmcsStatus.Describe(false, null));
    }

    [Fact]
    public void Describe_ShowsTheSkillGate_WhenOn()
    {
        var notBought = new FeatureToggle("Parry", initial: true);
        notBought.SetNeedsSkill(true);
        var bought = new FeatureToggle("Adrenaline", initial: true);
        bought.SetNeedsSkill(true);
        bought.SetHasSkill(true);
        var menuOff = new FeatureToggle("Charge Stagger", initial: false);
        menuOff.SetNeedsSkill(true);

        Assert.Equal(
            "Settings: Debug Logging=off | Parry=Enabled, needs its skill (not bought) | Adrenaline=Enabled, needs its skill (bought) | Charge Stagger=Disabled",
            DmcsStatus.Describe(false, new[] { notBought, bought, menuOff }));
    }
}
