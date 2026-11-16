using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class BodyHitDropRuleTests
{
    private static BodyHit Hit(
        bool enabled = true,
        bool modeOn = true,
        HitSource source = HitSource.HeldWeapon,
        bool targetIsPlayer = false,
        bool targetKilled = false,
        HitPart part = HitPart.Other,
        bool includeForearm = false,
        bool staggered = true,
        bool holdsDroppable = true,
        int earlierHits = 0,
        float roll = 0f,
        float baseChance = 0.25f,
        float perExtraHit = 0.25f) =>
        new BodyHit(enabled, modeOn, source, targetIsPlayer, targetKilled, part, includeForearm, staggered, holdsDroppable, earlierHits, roll, baseChance, perExtraHit);

    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void APlayerBodyHit_OnAStaggeredNpc_CanDrop(HitSource source)
    {
        Assert.Equal(BodyHitDecision.Drop, BodyHitDropRule.Decide(Hit(source: source)).Decision);
    }

    [Theory]
    [InlineData(0, 0.25f)]
    [InlineData(1, 0.5f)]
    [InlineData(2, 0.75f)]
    [InlineData(3, 1f)]
    [InlineData(7, 1f)]
    public void EachEarlierHit_InTheStagger_RaisesTheChance_UpToCertain(int earlierHits, float expected)
    {
        Assert.Equal(expected, BodyHitDropRule.Decide(Hit(earlierHits: earlierHits)).Chance, 5);
    }

    [Theory]
    [InlineData(0.24f, 0, BodyHitDecision.Drop)]
    [InlineData(0.25f, 0, BodyHitDecision.ChanceFailed)]
    [InlineData(0.49f, 1, BodyHitDecision.Drop)]
    [InlineData(0.5f, 1, BodyHitDecision.ChanceFailed)]
    public void TheRoll_IsComparedWithTheRaisedChance(float roll, int earlierHits, BodyHitDecision expected)
    {
        Assert.Equal(expected, BodyHitDropRule.Decide(Hit(roll: roll, earlierHits: earlierHits)).Decision);
    }

    [Fact]
    public void ZeroChances_NeverDrop()
    {
        Assert.Equal(BodyHitDecision.ChanceFailed, BodyHitDropRule.Decide(Hit(baseChance: 0f, perExtraHit: 0f, earlierHits: 5)).Decision);
    }

    [Theory]
    [InlineData(HitPart.LeftHand, false)]
    [InlineData(HitPart.RightHand, false)]
    [InlineData(HitPart.LeftArm, true)]
    [InlineData(HitPart.RightArm, true)]
    public void HitsThatDisarmStrikeHandles_AreNotBodyHits(HitPart part, bool includeForearm)
    {
        Assert.Equal(BodyHitDecision.DisarmPart, BodyHitDropRule.Decide(Hit(part: part, includeForearm: includeForearm)).Decision);
    }

    [Fact]
    public void AForearmHit_IsABodyHit_WhileForearmDoesNotDisarm()
    {
        Assert.Equal(BodyHitDecision.Drop, BodyHitDropRule.Decide(Hit(part: HitPart.RightArm)).Decision);
    }

    [Fact]
    public void TheRefusals_ComeInOrder()
    {
        Assert.Equal(BodyHitDecision.Disabled, BodyHitDropRule.Decide(Hit(enabled: false, source: HitSource.NotPlayer)).Decision);
        Assert.Equal(BodyHitDecision.Disabled, BodyHitDropRule.Decide(Hit(modeOn: false, source: HitSource.NotPlayer)).Decision);
        Assert.Equal(BodyHitDecision.NotByPlayer, BodyHitDropRule.Decide(Hit(source: HitSource.NotPlayer, targetIsPlayer: true)).Decision);
        Assert.Equal(BodyHitDecision.TargetIsPlayer, BodyHitDropRule.Decide(Hit(targetIsPlayer: true, targetKilled: true)).Decision);
        Assert.Equal(BodyHitDecision.TargetDead, BodyHitDropRule.Decide(Hit(targetKilled: true, part: HitPart.LeftHand)).Decision);
        Assert.Equal(BodyHitDecision.DisarmPart, BodyHitDropRule.Decide(Hit(part: HitPart.LeftHand, staggered: false)).Decision);
        Assert.Equal(BodyHitDecision.NotStaggered, BodyHitDropRule.Decide(Hit(staggered: false, holdsDroppable: false)).Decision);
        Assert.Equal(BodyHitDecision.NothingToDrop, BodyHitDropRule.Decide(Hit(holdsDroppable: false)).Decision);
    }

    [Theory]
    [InlineData(BodyHitDecision.Disabled, true)]
    [InlineData(BodyHitDecision.NotByPlayer, true)]
    [InlineData(BodyHitDecision.TargetIsPlayer, true)]
    [InlineData(BodyHitDecision.TargetDead, true)]
    [InlineData(BodyHitDecision.DisarmPart, true)]
    [InlineData(BodyHitDecision.NotStaggered, true)]
    [InlineData(BodyHitDecision.NothingToDrop, false)]
    [InlineData(BodyHitDecision.ChanceFailed, false)]
    [InlineData(BodyHitDecision.Drop, false)]
    public void OnlyBodyHitsOnAStaggeredNpc_AreLogged(BodyHitDecision decision, bool quiet)
    {
        Assert.Equal(quiet, BodyHitDropRule.IsQuiet(decision));
    }

    [Theory]
    [InlineData(BodyHitDecision.ChanceFailed, true)]
    [InlineData(BodyHitDecision.NothingToDrop, false)]
    [InlineData(BodyHitDecision.Drop, false)]
    [InlineData(BodyHitDecision.NotStaggered, false)]
    public void OnlyAMissedRoll_CountsTowardsTheNextHit(BodyHitDecision decision, bool counts)
    {
        Assert.Equal(counts, BodyHitDropRule.CountsAsEarlierHit(decision));
    }
}

public class StaggerHitCountsTests
{
    [Fact]
    public void Hits_AddUp_PerNpc()
    {
        var c = new StaggerHitCounts<int>();
        c.Add(1);
        c.Add(1);
        c.Add(2);

        Assert.Equal(2, c.Get(1));
        Assert.Equal(1, c.Get(2));
        Assert.Equal(0, c.Get(3));
    }

    [Fact]
    public void TheCount_EndsWithTheStagger()
    {
        var c = new StaggerHitCounts<int>();
        c.Add(1);
        c.Add(2);
        c.EndStaggers(id => id == 2);

        Assert.Equal(0, c.Get(1));
        Assert.Equal(1, c.Get(2));
    }

    [Fact]
    public void Forget_AndClear_DropCounts()
    {
        var c = new StaggerHitCounts<int>();
        c.Add(1);
        c.Add(2);
        c.Forget(1);
        Assert.Equal(0, c.Get(1));
        Assert.Equal(1, c.Count);
        c.Clear();
        Assert.Equal(0, c.Count);
    }
}

public class DisarmShieldTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void AShieldHand_StaysArmed_UnlessShieldsCanBeDisarmed(bool isShield, bool shieldsCanBeDisarmed, bool expected)
    {
        Assert.Equal(expected, DisarmShield.Keeps(isShield, shieldsCanBeDisarmed));
    }
}

public class BodyHitDropSettingsTests
{
    [Fact]
    public void Defaults_MatchTheSpec()
    {
        Assert.True(DisarmStrikeSettings.DefaultBodyHitDrop);
        Assert.Equal(25, DisarmStrikeSettings.DefaultBodyHitDropChance);
        Assert.Equal(25, DisarmStrikeSettings.DefaultDropChancePerExtraHit);
        Assert.False(DisarmStrikeSettings.DefaultShieldsCanBeDisarmed);
        Assert.True(DisarmStrikeSettings.DefaultLoosenedSpark);
        Assert.True(DisarmStrikeSettings.DefaultLoosenedHaptics);
    }

    [Fact]
    public void ChanceDefaults_AreOnTheChanceSlider()
    {
        Assert.Contains(DisarmStrikeSettings.DefaultBodyHitDropChance, DisarmStrikeSettings.ChanceValues());
        Assert.Contains(DisarmStrikeSettings.DefaultDropChancePerExtraHit, DisarmStrikeSettings.ChanceValues());
    }

    [Fact]
    public void FlingDefault_IsOneOfItsValues_AndZeroMeansJustDrop()
    {
        Assert.True(SliderValues.IndexOf(DisarmStrikeSettings.FlingForce.Values(), DisarmStrikeSettings.FlingForce.Default) >= 0);
        Assert.Equal(0f, DisarmStrikeSettings.FlingForce.Min);
    }
}

public class BodyHitNpcChanceTests
{
    [Theory]
    [InlineData(false, false, 0.25f)]
    [InlineData(true, false, 0.5f)]
    [InlineData(false, true, 0.75f)]
    [InlineData(true, true, 0.75f)]
    public void EachNpcType_HasItsOwnChance_AndTheHighestApplies(bool isFemale, bool isSmaller, float expected)
    {
        Assert.Equal(expected, BodyHitDropRule.NpcChance(0.25f, isFemale, 0.5f, isSmaller, 0.75f));
    }

    [Fact]
    public void ALowerTypeChance_DoesNotLowerTheBaseChance()
    {
        Assert.Equal(0.25f, BodyHitDropRule.NpcChance(0.25f, true, 0f, true, 0.1f));
    }

    [Fact]
    public void BaseZero_WithAFemaleChance_WorksOnlyOnFemaleNpcs()
    {
        Assert.Equal(0f, BodyHitDropRule.NpcChance(0f, false, 0.25f, false, 0f));
        Assert.Equal(0.25f, BodyHitDropRule.NpcChance(0f, true, 0.25f, false, 0f));
    }

    [Fact]
    public void TheExtraHitBonus_AddsToTheNpcsChance()
    {
        var hit = new BodyHit(true, true, HitSource.HeldWeapon, false, false, HitPart.Other, false, true, true, 1, 0.99f,
            BodyHitDropRule.NpcChance(0.25f, true, 0.5f, false, 0.25f), 0.25f);

        Assert.Equal(0.75f, BodyHitDropRule.Decide(hit).Chance, 5);
    }
}

public class SizeFilterTests
{
    [Theory]
    [InlineData(1.6f, 1.8f, 0f, true)]
    [InlineData(1.8f, 1.8f, 0f, false)]
    [InlineData(1.9f, 1.8f, 0f, false)]
    [InlineData(1.7f, 1.8f, 0.1f, false)]
    [InlineData(1.69f, 1.8f, 0.1f, true)]
    [InlineData(1.6f, 0f, 0f, false)]
    [InlineData(0f, 1.8f, 0f, false)]
    public void Smaller_MeansShorterThanThePlayer_ByAtLeastTheMargin(float npc, float player, float margin, bool expected)
    {
        Assert.Equal(expected, SizeFilter.IsSmaller(npc, player, margin));
    }

    [Fact]
    public void MarginDefault_IsOneOfItsValues_AndZero()
    {
        var s = DisarmStrikeSettings.SmallerMargin;
        Assert.Equal(0f, s.Default);
        Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0);
    }

    [Fact]
    public void TypeChances_DefaultToTheBaseChance_SoTheyChangeNothingUntilSet()
    {
        Assert.Equal(DisarmStrikeSettings.DefaultBodyHitDropChance, DisarmStrikeSettings.DefaultFemaleChance);
        Assert.Equal(DisarmStrikeSettings.DefaultBodyHitDropChance, DisarmStrikeSettings.DefaultSmallerChance);
        Assert.Contains(DisarmStrikeSettings.DefaultFemaleChance, DisarmStrikeSettings.ChanceValues());
        Assert.Contains(DisarmStrikeSettings.DefaultSmallerChance, DisarmStrikeSettings.ChanceValues());
    }
}
