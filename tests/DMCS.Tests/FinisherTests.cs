using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// Finisher on a downed enemy (user 2026-11-23, default on; Dark Messiah: stab a knocked-down enemy in the chest, without
/// adrenaline): a player stab with a held weapon into the torso of a living enemy lying on the ground kills it.
/// </summary>
public class FinisherTests
{
    private static FinisherHit Hit(
        bool enabled = true, HitSource source = HitSource.HeldWeapon, bool targetIsPlayer = false, bool targetDead = false,
        bool downed = true, bool kneeling = false, bool torso = true, bool stab = true) =>
        new FinisherHit(enabled, source, targetIsPlayer, targetDead, downed, kneeling, torso, stab);

    [Fact]
    public void Finish_AStabIntoTheChestOfADownedEnemy()
    {
        Assert.Equal(FinisherDecision.Finish, FinisherRule.Decide(Hit()));
    }

    [Fact]
    public void Disabled_ComesFirst()
    {
        Assert.Equal(FinisherDecision.Disabled, FinisherRule.Decide(Hit(enabled: false, source: HitSource.NotPlayer)));
    }

    [Theory]
    [InlineData(HitSource.NotPlayer)]
    [InlineData(HitSource.PlayerBody)]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void OnlyAHeldWeapon(HitSource source)
    {
        Assert.Equal(FinisherDecision.NotAHeldWeapon, FinisherRule.Decide(Hit(source: source, targetIsPlayer: true)));
    }

    [Fact]
    public void TargetIsPlayer()
    {
        Assert.Equal(FinisherDecision.TargetIsPlayer, FinisherRule.Decide(Hit(targetIsPlayer: true, targetDead: true)));
    }

    [Fact]
    public void TargetDead()
    {
        Assert.Equal(FinisherDecision.TargetDead, FinisherRule.Decide(Hit(targetDead: true, downed: false)));
    }

    [Fact]
    public void Standing_NotDowned()
    {
        Assert.Equal(FinisherDecision.NotDowned, FinisherRule.Decide(Hit(downed: false, kneeling: true)));
    }

    [Fact]
    public void Kneeling_IsTheExecutionsJob()
    {
        Assert.Equal(FinisherDecision.Kneeling, FinisherRule.Decide(Hit(kneeling: true, torso: false)));
    }

    [Fact]
    public void NotTheTorso()
    {
        Assert.Equal(FinisherDecision.NotTorso, FinisherRule.Decide(Hit(torso: false, stab: false)));
    }

    [Fact]
    public void ACutIsNoStab()
    {
        Assert.Equal(FinisherDecision.NotAStab, FinisherRule.Decide(Hit(stab: false)));
    }

    [Fact]
    public void Settings_DefaultOn()
    {
        Assert.True(FinisherSettings.DefaultEnabled);
        Assert.Equal(DmcsCategories.Finisher + " Enabled", FinisherSettings.EnabledName);
    }
}
