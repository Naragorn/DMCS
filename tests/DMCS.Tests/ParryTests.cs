using System;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ParryRuleTests
{
    // Defaults as in the Mod Menu: counter from 1 m/s, power from 3 m/s, power chance 100 %.
    private static ParryContact C(
        bool enabled = true, bool counter = true, bool power = true, bool playerParry = true, bool clash = false,
        bool holds = true, bool shield = false, float speed = 0f, float counterMin = 1f, float powerMin = 3f,
        float roll = 0.1f, float chance = 1f) =>
        new ParryContact(enabled, counter, power, playerParry, clash, holds, shield, speed, counterMin, powerMin, roll, chance);

    [Fact]
    public void AHeldGuard_IsAnOrdinaryParry()
    {
        var v = ParryRule.Decide(C(speed: 0.2f));

        Assert.Equal(ParryDecision.TooSlow, v.Decision);
        Assert.False(v.Staggers);
        Assert.False(v.Disarms);
    }

    [Fact]
    public void AGuardBroughtUpInTime_IsACounter_ThatStaggers()
    {
        var v = ParryRule.Decide(C(speed: 1.5f));

        Assert.Equal(ParryDecision.Counter, v.Decision);
        Assert.True(v.Staggers);
        Assert.False(v.Disarms);
    }

    [Fact]
    public void AHardSwingIntoTheAttack_IsAPowerParry_ThatDisarmsAndStaggers()
    {
        var v = ParryRule.Decide(C(speed: 5f));

        Assert.Equal(ParryDecision.PowerParry, v.Decision);
        Assert.True(v.Disarms);
        Assert.True(v.Staggers);
    }

    [Fact]
    public void ExactlyTheMinimum_Counts()
    {
        Assert.Equal(ParryDecision.Counter, ParryRule.Decide(C(speed: 1f)).Decision);
        Assert.Equal(ParryDecision.PowerParry, ParryRule.Decide(C(speed: 3f)).Decision);
        Assert.Equal(ParryDecision.Counter, ParryRule.Decide(C(speed: 2.99f)).Decision);
        Assert.Equal(ParryDecision.TooSlow, ParryRule.Decide(C(speed: 0.99f)).Decision);
    }

    [Fact]
    public void PowerParryOff_AHardSwingIsACounter()
    {
        var v = ParryRule.Decide(C(power: false, speed: 5f));

        Assert.Equal(ParryDecision.Counter, v.Decision);
        Assert.False(v.Disarms);
    }

    [Fact]
    public void CounterOff_APowerParryStillDisarms_ButDoesNotStagger_AndASlowerGuardDoesNothing()
    {
        var hard = ParryRule.Decide(C(counter: false, speed: 5f));
        var guard = ParryRule.Decide(C(counter: false, speed: 1.5f));

        Assert.Equal(ParryDecision.PowerParry, hard.Decision);
        Assert.False(hard.Staggers);
        Assert.Equal(ParryDecision.TooSlow, guard.Decision);
        Assert.False(guard.Staggers);
    }

    [Fact]
    public void ACounterMinAboveThePowerMin_StaggersOnlyFromItsOwnMinimum()
    {
        var v = ParryRule.Decide(C(speed: 3.5f, counterMin: 4f));

        Assert.Equal(ParryDecision.PowerParry, v.Decision);
        Assert.False(v.Staggers);
    }

    [Theory]
    [InlineData(ParryDecision.KeepsShield)]
    [InlineData(ParryDecision.NoWeapon)]
    [InlineData(ParryDecision.PowerChanceFailed)]
    public void AHardSwingThatCannotDisarm_SaysWhy_AndStillCounters(ParryDecision expected)
    {
        var c = expected switch
        {
            ParryDecision.KeepsShield => C(speed: 5f, shield: true),
            ParryDecision.NoWeapon => C(speed: 5f, holds: false),
            _ => C(speed: 5f, roll: 0.5f, chance: 0.5f),
        };

        var v = ParryRule.Decide(c);

        Assert.Equal(expected, v.Decision);
        Assert.False(v.Disarms);
        Assert.True(v.Staggers);
    }

    [Fact]
    public void Chance_ZeroNeverDisarms_HundredAlways()
    {
        Assert.Equal(ParryDecision.PowerChanceFailed, ParryRule.Decide(C(speed: 5f, roll: 0f, chance: 0f)).Decision);
        Assert.Equal(ParryDecision.PowerParry, ParryRule.Decide(C(speed: 5f, roll: 0.999f, chance: 1f)).Decision);
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void Refusals_ComeBeforeAnySpeed(bool enabled, bool counter, bool power, bool playerParry, bool clash)
    {
        var v = ParryRule.Decide(C(enabled, counter, power, playerParry, clash, speed: 9f));

        var expected = !enabled || (!counter && !power) ? ParryDecision.Disabled
            : !playerParry ? ParryDecision.NotPlayerParry
            : ParryDecision.TakenByClash;
        Assert.Equal(expected, v.Decision);
        Assert.False(v.Disarms);
        Assert.False(v.Staggers);
    }

    [Fact]
    public void OnlyRealPlayerParries_AreLogged()
    {
        Assert.True(ParryRule.IsQuiet(ParryDecision.Disabled));
        Assert.True(ParryRule.IsQuiet(ParryDecision.NotPlayerParry));
        foreach (var d in new[]
        {
            ParryDecision.TakenByClash, ParryDecision.PowerParry, ParryDecision.KeepsShield, ParryDecision.NoWeapon,
            ParryDecision.PowerChanceFailed, ParryDecision.Counter, ParryDecision.TooSlow,
        })
        {
            Assert.False(ParryRule.IsQuiet(d), d.ToString());
        }
    }

    [Fact]
    public void Settings_AsSpecified()
    {
        Assert.True(ParrySettings.DefaultEnabled);
        Assert.True(ParrySettings.DefaultCounter);
        Assert.True(ParrySettings.DefaultPower);
        Assert.Equal(1f, ParrySettings.CounterSpeed.Default);
        Assert.Equal(3f, ParrySettings.PowerSpeed.Default);
        Assert.Equal(100f, ParrySettings.PowerChance.Default);
        Assert.Equal(1.5f, ParrySettings.StaggerDuration.Default);
        Assert.Equal(0.2f, ParrySettings.SpeedWindow.Default);
        Assert.True(ParrySettings.CounterSpeed.Default < ParrySettings.PowerSpeed.Default);
        foreach (var s in ParrySettings.Sliders)
        {
            Assert.Contains(s.Values(), v => Math.Abs(v - s.Default) < 0.001f);
        }
    }
}
