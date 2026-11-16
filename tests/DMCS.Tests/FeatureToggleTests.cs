using System.Collections.Generic;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class FeatureToggleTests
{
    [Fact]
    public void StartsWithGivenDefault()
    {
        var toggle = new FeatureToggle("Charge Stagger", initial: true);

        Assert.True(toggle.Enabled);
        Assert.Equal("Charge Stagger", toggle.Name);
    }

    [Fact]
    public void Set_ToDifferentValue_ChangesStateAndRaisesChanged()
    {
        var toggle = new FeatureToggle("Adrenaline", initial: true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        var changed = toggle.Set(false);

        Assert.True(changed);
        Assert.False(toggle.Enabled);
        Assert.Equal(new[] { false }, seen);
    }

    [Fact]
    public void Set_ToSameValue_IsNoOp_AndDoesNotRaiseChanged()
    {
        var toggle = new FeatureToggle("Adrenaline", initial: true);
        var raised = 0;
        toggle.Changed += _ => raised++;

        var changed = toggle.Set(true);

        Assert.False(changed);
        Assert.True(toggle.Enabled);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Set_WithoutSubscribers_DoesNotThrow()
    {
        var toggle = new FeatureToggle("Disarm Strike", initial: false);

        Assert.True(toggle.Set(true));
    }

    [Fact]
    public void Set_LogsTheTransition_WhenDebugLoggingIsOn()
    {
        var lines = new List<string>();
        var log = new DmcsLog(lines.Add) { DebugEnabled = true };
        var toggle = new FeatureToggle("Clash of Might", initial: true, log);

        toggle.Set(false);
        toggle.Set(false);

        Assert.Equal(new[] { "[DMCS][Clash of Might] Disabled" }, lines);
    }
}
