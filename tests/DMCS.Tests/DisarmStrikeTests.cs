using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class DisarmRuleTests
{
    private static DisarmHit Hit(
        bool enabled = true,
        bool finalPhase = true,
        HitSource source = HitSource.HeldWeapon,
        bool targetIsPlayer = false,
        bool targetKilled = false,
        HitPart part = HitPart.RightHand,
        bool includeForearm = false,
        bool requireStagger = true,
        bool staggered = true,
        bool holdsSomething = true,
        float roll = 0f,
        float chance = 1f) =>
        new DisarmHit(enabled, finalPhase, source, targetIsPlayer, targetKilled, part, includeForearm, requireStagger, staggered, holdsSomething, roll, chance);

    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void PlayerHit_OnAStaggeredNpcsHand_Disarms(HitSource source)
    {
        Assert.Equal(DisarmDecision.Disarm, DisarmRule.Decide(Hit(source: source)));
    }

    [Theory]
    [InlineData(HitPart.LeftHand)]
    [InlineData(HitPart.RightHand)]
    public void BothHands_Count(HitPart part)
    {
        Assert.Equal(DisarmDecision.Disarm, DisarmRule.Decide(Hit(part: part)));
    }

    [Theory]
    [InlineData(HitPart.LeftArm, false, DisarmDecision.WrongPart)]
    [InlineData(HitPart.RightArm, false, DisarmDecision.WrongPart)]
    [InlineData(HitPart.LeftArm, true, DisarmDecision.Disarm)]
    [InlineData(HitPart.RightArm, true, DisarmDecision.Disarm)]
    [InlineData(HitPart.Other, true, DisarmDecision.WrongPart)]
    public void Forearm_CountsOnlyWhenSwitchedOn(HitPart part, bool includeForearm, DisarmDecision expected)
    {
        Assert.Equal(expected, DisarmRule.Decide(Hit(part: part, includeForearm: includeForearm)));
    }

    [Fact]
    public void NotStaggered_IsRefused_OnlyWhileStaggerIsRequired()
    {
        Assert.Equal(DisarmDecision.NotStaggered, DisarmRule.Decide(Hit(staggered: false)));
        Assert.Equal(DisarmDecision.Disarm, DisarmRule.Decide(Hit(staggered: false, requireStagger: false)));
    }

    [Theory]
    [InlineData(0.49f, 0.5f, DisarmDecision.Disarm)]
    [InlineData(0.50f, 0.5f, DisarmDecision.ChanceFailed)]
    [InlineData(0.00f, 0.0f, DisarmDecision.ChanceFailed)]
    public void Chance_RollBelowTheChanceSucceeds(float roll, float chance, DisarmDecision expected)
    {
        Assert.Equal(expected, DisarmRule.Decide(Hit(roll: roll, chance: chance)));
    }

    [Theory]
    [InlineData(false, true, HitSource.HeldWeapon, false, false, true, DisarmDecision.Disabled)]
    [InlineData(true, false, HitSource.HeldWeapon, false, false, true, DisarmDecision.NotFinalPhase)]
    [InlineData(true, true, HitSource.NotPlayer, false, false, true, DisarmDecision.NotByPlayer)]
    [InlineData(true, true, HitSource.HeldWeapon, true, false, true, DisarmDecision.TargetIsPlayer)]
    [InlineData(true, true, HitSource.HeldWeapon, false, true, true, DisarmDecision.TargetDead)]
    [InlineData(true, true, HitSource.HeldWeapon, false, false, false, DisarmDecision.NothingHeld)]
    public void EveryRefusal_HasItsOwnReason(bool enabled, bool finalPhase, HitSource source, bool targetIsPlayer, bool killed, bool holds, DisarmDecision expected)
    {
        Assert.Equal(expected, DisarmRule.Decide(Hit(enabled, finalPhase, source, targetIsPlayer, killed, holdsSomething: holds)));
    }

    [Theory]
    [InlineData(DisarmDecision.Disarm, false)]
    [InlineData(DisarmDecision.WrongPart, false)]
    [InlineData(DisarmDecision.NotStaggered, false)]
    [InlineData(DisarmDecision.NothingHeld, false)]
    [InlineData(DisarmDecision.ChanceFailed, false)]
    [InlineData(DisarmDecision.Disabled, true)]
    [InlineData(DisarmDecision.NotFinalPhase, true)]
    [InlineData(DisarmDecision.NotByPlayer, true)]
    [InlineData(DisarmDecision.TargetIsPlayer, true)]
    [InlineData(DisarmDecision.TargetDead, true)]
    public void OnlyPlayerHitsOnLivingNpcs_AreWorthLogging(DisarmDecision decision, bool quiet)
    {
        Assert.Equal(quiet, DisarmRule.IsQuiet(decision));
    }
}

public class DisarmHandsTests
{
    [Theory]
    [InlineData(HitPart.RightHand, false, true, false)]
    [InlineData(HitPart.RightArm, false, true, false)]
    [InlineData(HitPart.LeftHand, false, false, true)]
    [InlineData(HitPart.LeftArm, false, false, true)]
    [InlineData(HitPart.RightHand, true, true, true)]
    [InlineData(HitPart.LeftArm, true, true, true)]
    public void TheHitSideDrops_BothWhenSwitchedOn(HitPart part, bool dropBoth, bool right, bool left)
    {
        var hands = DisarmHands.ToRelease(part, dropBoth);

        Assert.Equal(right, hands.Right);
        Assert.Equal(left, hands.Left);
    }

    [Fact]
    public void OtherParts_DropNothing()
    {
        var hands = DisarmHands.ToRelease(HitPart.Other, true);

        Assert.False(hands.Right);
        Assert.False(hands.Left);
    }
}

public class DisarmSettingsTests
{
    [Fact]
    public void NoRegrabDefault_IsOneOfItsSliderValues()
    {
        var s = DisarmStrikeSettings.NoRegrabTime;

        Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0);
    }

    [Theory]
    [InlineData(false, 10f, 10f)]
    [InlineData(true, 10f, float.PositiveInfinity)]
    public void Permanent_MeansForever(bool permanent, float seconds, float expected)
    {
        Assert.Equal(expected, DisarmStrikeSettings.NoRegrabSeconds(permanent, seconds));
    }

    [Fact]
    public void ChanceValues_AreZeroToHundredInFives()
    {
        Assert.Equal(21, DisarmStrikeSettings.ChanceValues().Length);
        Assert.Contains(DisarmStrikeSettings.DefaultChancePercent, DisarmStrikeSettings.ChanceValues());
    }
}

public class StaggerWindowsForeverTests
{
    [Fact]
    public void AnEndlessWindow_NeverExpires()
    {
        var w = new StaggerWindows<int>();
        w.Mark(1, 0f, float.PositiveInfinity);

        Assert.True(w.IsStaggered(1, 1e9f));
        Assert.Empty(w.TakeExpired(1e9f));
    }
}
