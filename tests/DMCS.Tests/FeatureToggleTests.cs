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

    // Skill tree gate (2026-11-20): a feature can need its skill from the game's Body tree. Enabled is then the menu's
    // Enabled and the skill; MenuEnabled stays what the menu shows and saves.

    [Fact]
    public void WithoutTheGate_TheSkillChangesNothing()
    {
        var toggle = new FeatureToggle("Parry", initial: true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.False(toggle.SetHasSkill(true));
        Assert.False(toggle.SetHasSkill(false));

        Assert.True(toggle.Enabled);
        Assert.False(toggle.NeedsSkill);
        Assert.Empty(seen);
    }

    [Fact]
    public void Gate_On_WithoutTheSkill_SwitchesTheFeatureOff_ButNotTheMenu()
    {
        var lines = new List<string>();
        var toggle = new FeatureToggle("Parry", initial: true, new DmcsLog(lines.Add) { DebugEnabled = true });
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.True(toggle.SetNeedsSkill(true));

        Assert.False(toggle.Enabled);
        Assert.True(toggle.MenuEnabled);
        Assert.Equal(new[] { false }, seen);
        Assert.Equal(new[] { "[DMCS][Parry] Disabled (needs its skill, not bought)" }, lines);
    }

    [Fact]
    public void Gate_On_BuyingTheSkill_SwitchesItOn_AndRefundingOffAgain()
    {
        var lines = new List<string>();
        var toggle = new FeatureToggle("Parry", initial: true, new DmcsLog(lines.Add) { DebugEnabled = true });
        toggle.SetNeedsSkill(true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;
        lines.Clear();

        Assert.True(toggle.SetHasSkill(true));
        Assert.True(toggle.Enabled);
        Assert.True(toggle.SetHasSkill(false));
        Assert.False(toggle.Enabled);

        Assert.Equal(new[] { true, false }, seen);
        Assert.Equal(new[] { "[DMCS][Parry] Enabled (skill bought)", "[DMCS][Parry] Disabled (needs its skill, not bought)" }, lines);
    }

    [Fact]
    public void Gate_On_WithTheSkill_StaysOn()
    {
        var toggle = new FeatureToggle("Parry", initial: true);
        toggle.SetHasSkill(true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.False(toggle.SetNeedsSkill(true));

        Assert.True(toggle.Enabled);
        Assert.Empty(seen);
    }

    [Fact]
    public void Gate_On_MenuOff_StaysOff_WhateverTheSkill()
    {
        var toggle = new FeatureToggle("Parry", initial: false);
        toggle.SetNeedsSkill(true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.False(toggle.SetHasSkill(true));
        Assert.False(toggle.SetHasSkill(false));

        Assert.False(toggle.Enabled);
        Assert.Empty(seen);
    }

    [Fact]
    public void Gate_On_NoSkill_TheMenuToggleChangesOnlyTheMenu()
    {
        var toggle = new FeatureToggle("Parry", initial: true);
        toggle.SetNeedsSkill(true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.False(toggle.Set(false));
        Assert.False(toggle.MenuEnabled);
        Assert.False(toggle.Set(true));
        Assert.True(toggle.MenuEnabled);

        Assert.False(toggle.Enabled);
        Assert.Empty(seen);
    }

    [Fact]
    public void Gate_Off_AgainWithoutTheSkill_SwitchesTheFeatureBackOn()
    {
        var toggle = new FeatureToggle("Parry", initial: true);
        toggle.SetNeedsSkill(true);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.True(toggle.SetNeedsSkill(false));
        Assert.False(toggle.SetNeedsSkill(false));

        Assert.True(toggle.Enabled);
        Assert.Equal(new[] { true }, seen);
    }

    [Fact]
    public void Gate_MenuOff_SwitchingTheGateChangesNothing()
    {
        var toggle = new FeatureToggle("Parry", initial: false);
        var seen = new List<bool>();
        toggle.Changed += seen.Add;

        Assert.False(toggle.SetNeedsSkill(true));
        Assert.True(toggle.NeedsSkill);

        Assert.False(toggle.Enabled);
        Assert.Empty(seen);
    }
}
