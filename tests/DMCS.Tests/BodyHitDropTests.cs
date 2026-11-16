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

public class BodyHitFilterTests
{
    private static BodyHit Hit(
        bool staggered = true,
        bool femaleOnly = false,
        bool isFemale = false,
        bool smallerOnly = false,
        bool isSmaller = false,
        bool surrendered = false,
        float roll = 0.99f,
        bool holdsDroppable = true) =>
        new BodyHit(true, true, HitSource.HeldWeapon, false, false, HitPart.Other, false, staggered, holdsDroppable, 0, roll, 0.25f, 0.25f,
            femaleOnly, isFemale, smallerOnly, isSmaller, surrendered);

    [Theory]
    [InlineData(false, false, BodyHitDecision.ChanceFailed)]
    [InlineData(true, false, BodyHitDecision.NotFemale)]
    [InlineData(true, true, BodyHitDecision.ChanceFailed)]
    public void FemaleOnly_SkipsOtherNpcs(bool femaleOnly, bool isFemale, BodyHitDecision expected)
    {
        Assert.Equal(expected, BodyHitDropRule.Decide(Hit(femaleOnly: femaleOnly, isFemale: isFemale)).Decision);
    }

    [Theory]
    [InlineData(false, false, BodyHitDecision.ChanceFailed)]
    [InlineData(true, false, BodyHitDecision.NotSmaller)]
    [InlineData(true, true, BodyHitDecision.ChanceFailed)]
    public void SmallerOnly_SkipsNpcsAsTallAsThePlayer(bool smallerOnly, bool isSmaller, BodyHitDecision expected)
    {
        Assert.Equal(expected, BodyHitDropRule.Decide(Hit(smallerOnly: smallerOnly, isSmaller: isSmaller)).Decision);
    }

    [Fact]
    public void BothFilters_MustPass()
    {
        Assert.Equal(BodyHitDecision.NotSmaller, BodyHitDropRule.Decide(Hit(femaleOnly: true, isFemale: true, smallerOnly: true)).Decision);
        Assert.Equal(BodyHitDecision.NotFemale, BodyHitDropRule.Decide(Hit(femaleOnly: true, smallerOnly: true, isSmaller: true)).Decision);
    }

    [Fact]
    public void ASurrenderedNpc_DropsForCertain_WithoutStagger()
    {
        var r = BodyHitDropRule.Decide(Hit(staggered: false, surrendered: true, roll: 0.99f));

        Assert.Equal(BodyHitDecision.Drop, r.Decision);
        Assert.Equal(1f, r.Chance);
    }

    [Fact]
    public void Filters_StillApply_ToASurrenderedNpc()
    {
        Assert.Equal(BodyHitDecision.NotFemale, BodyHitDropRule.Decide(Hit(surrendered: true, femaleOnly: true)).Decision);
    }

    [Fact]
    public void NotStaggered_ComesBeforeTheFilters_SoUnstaggeredHitsStayQuiet()
    {
        Assert.Equal(BodyHitDecision.NotStaggered, BodyHitDropRule.Decide(Hit(staggered: false, femaleOnly: true)).Decision);
    }

    [Theory]
    [InlineData(BodyHitDecision.NotFemale)]
    [InlineData(BodyHitDecision.NotSmaller)]
    public void FilterRefusals_AreLogged(BodyHitDecision decision)
    {
        Assert.False(BodyHitDropRule.IsQuiet(decision));
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
    public void Defaults_MatchTheRequest()
    {
        Assert.False(DisarmStrikeSettings.DefaultFemaleOnly);
        Assert.False(DisarmStrikeSettings.DefaultSmallerOnly);
        Assert.True(DisarmStrikeSettings.DefaultSurrenderAlwaysDrops);
    }
}

public class DisarmSurrenderTests
{
    private static DisarmHit Hit(bool staggered, bool surrendered, float roll, float chance) =>
        new DisarmHit(true, true, HitSource.HeldWeapon, false, false, HitPart.RightHand, false, true, staggered, true, roll, chance, surrendered);

    [Fact]
    public void ASurrenderedNpcsHand_Disarms_WithoutStagger_AndForCertain()
    {
        Assert.Equal(DisarmDecision.Disarm, DisarmRule.Decide(Hit(false, true, 0.99f, 0f)));
    }

    [Fact]
    public void WithoutSurrender_TheRulesStay()
    {
        Assert.Equal(DisarmDecision.NotStaggered, DisarmRule.Decide(Hit(false, false, 0f, 1f)));
        Assert.Equal(DisarmDecision.ChanceFailed, DisarmRule.Decide(Hit(true, false, 0.99f, 0.5f)));
    }
}
