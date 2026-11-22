using System;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

// Clash Hold Blade's styles and options (user 2026-11-21/22): Hard or Spring, the rotation held or free, the whole lock
// or only the first moment.
public class ClashHoldTests
{
    [Fact]
    public void Styles_AreSpringAndHard_SpringByDefault()
    {
        Assert.Equal(new[] { "Spring", "Hard" }, ClashHold.Styles);
        Assert.Equal(ClashHold.Spring, ClashSettings.DefaultHoldStyle);
    }

    [Theory]
    [InlineData("Hard", 0f, true)]
    [InlineData("Hard", 0.02f, false)]
    [InlineData("Spring", 0f, false)]
    [InlineData("Spring", 0.02f, false)]
    public void OnlyHardWithoutSlack_IsRigid(string style, float slack, bool rigid) =>
        Assert.Equal(rigid, ClashHold.IsRigid(style, slack));

    [Theory]
    [InlineData("Spring", true)]
    [InlineData("Hard", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlySpring_IsSoft(string style, bool soft) =>
        Assert.Equal(soft, ClashHold.IsSoft(style));

    [Fact]
    public void Spring_IsSizedByTheBladesMass_SoItSpringsAlikeAtAnyWeight()
    {
        var (spring, damper) = ClashHold.SpringFor(30f, 3f, 0.5f);
        var omega = 2.0 * Math.PI * 3.0;

        Assert.Equal(30.0 * omega * omega, spring, 1);
        Assert.Equal(2.0 * 0.5 * 30.0 * omega, damper, 2);
    }

    [Fact]
    public void Spring_ScalesWithMass_LinearlyInBothTerms()
    {
        var light = ClashHold.SpringFor(10f, 3f, 0.5f);
        var heavy = ClashHold.SpringFor(20f, 3f, 0.5f);

        Assert.Equal(2f * light.Spring, heavy.Spring, 2);
        Assert.Equal(2f * light.Damper, heavy.Damper, 2);
    }

    [Fact]
    public void NoDamping_GivesNoDamper()
    {
        var (_, damper) = ClashHold.SpringFor(30f, 3f, 0f);

        Assert.Equal(0f, damper);
    }

    [Fact]
    public void NoMass_GivesNoSpring_NeverAHardLimitByAccident()
    {
        // A spring of 0 would make Unity's limit hard; the caller treats a zero spring as "no body mass known".
        var (spring, damper) = ClashHold.SpringFor(0f, 3f, 0.5f);

        Assert.Equal(0f, spring);
        Assert.Equal(0f, damper);
    }

    [Theory]
    [InlineData(false, 0f, true)]
    [InlineData(false, 5f, true)]
    [InlineData(true, 0f, true)]
    [InlineData(true, 0.29f, true)]
    [InlineData(true, 0.3f, false)]
    [InlineData(true, 2f, false)]
    public void Briefly_HoldsOnlyTheFirstMoment(bool briefly, float elapsed, bool holding) =>
        Assert.Equal(holding, ClashHold.StillHolding(elapsed, briefly, 0.3f));

    [Fact]
    public void Defaults_AsDesigned()
    {
        Assert.Equal(3f, ClashSettings.HoldSpring.Default);
        Assert.Equal(0.5f, ClashSettings.HoldSpringDamping.Default);
        Assert.False(ClashSettings.DefaultHoldRotation);
        Assert.False(ClashSettings.DefaultHoldBriefly);
        Assert.Equal(0.3f, ClashSettings.HoldTime.Default);
        Assert.Contains(ClashSettings.HoldSpring, ClashSettings.Sliders);
        Assert.Contains(ClashSettings.HoldSpringDamping, ClashSettings.Sliders);
        Assert.Contains(ClashSettings.HoldTime, ClashSettings.Sliders);
    }
}
