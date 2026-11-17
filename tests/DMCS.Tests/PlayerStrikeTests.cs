using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// A contact of the player's body with an NPC counts as the player's strike only when the player moved: found 2026-11-17,
/// a fighting NPC swinging into the player's motionless hand was read as a punch by that hand and disarmed itself.
/// </summary>
public class PlayerStrikeRuleTests
{
    private const float Min = 1.5f;

    [Fact]
    public void Hand_SwungFastEnough_IsAStrike() => Assert.True(PlayerStrikeRule.IsStrike(BodyPartKind.HandOrArm, 2f, Min));

    [Fact]
    public void Hand_AtTheThreshold_IsAStrike() => Assert.True(PlayerStrikeRule.IsStrike(BodyPartKind.HandOrArm, Min, Min));

    [Fact]
    public void Hand_HeldStill_IsNoStrike_EvenWhenTheContactWasHard() => Assert.False(PlayerStrikeRule.IsStrike(BodyPartKind.HandOrArm, 0.2f, Min));

    [Fact]
    public void FootOrLeg_AlwaysCounts_AsAKick() => Assert.True(PlayerStrikeRule.IsStrike(BodyPartKind.FootOrLeg, 0f, Min));

    [Fact]
    public void TorsoOrHead_NeverCounts() => Assert.False(PlayerStrikeRule.IsStrike(BodyPartKind.Other, 10f, Min));

    [Fact]
    public void ThresholdZero_TakesEveryHandContact_AsBefore() => Assert.True(PlayerStrikeRule.IsStrike(BodyPartKind.HandOrArm, 0f, 0f));

    [Fact]
    public void Default_IsOnItsSlider()
    {
        Assert.Equal(1.5f, PlayerStrikeRule.MinHandSpeed.Default);
        Assert.True(SliderValues.IndexOf(PlayerStrikeRule.MinHandSpeed.Values(), PlayerStrikeRule.MinHandSpeed.Default) >= 0);
    }
}

public class HandSpeedMeterTests
{
    [Fact]
    public void Vertical_Movement_Counts_In3D()
    {
        var m = new HeadSpeedMeter(0.05f);
        m.Add(0f, 0f, 0f, 0f);

        Assert.Equal(3f, m.Add(0.05f, 0f, 0.15f, 0f)!.Value, 3);
    }

    [Fact]
    public void TheHorizontalOverload_IgnoresHeight()
    {
        var m = new HeadSpeedMeter(0.1f);
        m.Add(0f, 0f, 0f);

        Assert.Equal(1f, m.Add(0.1f, 0.1f, 0f)!.Value, 3);
    }
}
