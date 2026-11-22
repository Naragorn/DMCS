using System.Linq;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// Adrenaline magic (user 2026-11-20): while the adrenaline window is open, the next player spell that hits an enemy is
/// the critical, with a big version per element instead of the melee fatality.
/// </summary>
public class AdrenalineMagicTests
{
    private static MagicCast Cast(
        bool adrenalineOn = true, bool magicOn = true, bool ready = true, bool bySpell = true,
        MagicElement element = MagicElement.Fire, bool elementOn = true, bool targetIsPlayer = false, bool targetDead = false) =>
        new MagicCast(adrenalineOn, magicOn, ready, bySpell, element, elementOn, targetIsPlayer, targetDead);

    [Theory]
    [InlineData("Fire", MagicElement.Fire)]
    [InlineData("Lightning", MagicElement.Lightning)]
    [InlineData("Gravity", MagicElement.Gravity)]
    [InlineData("Telekinesis", MagicElement.None)]
    [InlineData("SlowTime", MagicElement.None)]
    [InlineData("fire", MagicElement.None)]
    [InlineData("", MagicElement.None)]
    [InlineData(null, MagicElement.None)]
    public void ElementOf_TheGamesSpellIds(string id, MagicElement expected)
    {
        Assert.Equal(expected, MagicRule.ElementOf(id));
    }

    [Fact]
    public void Magic_WhenEverythingFits()
    {
        Assert.Equal(MagicDecision.Magic, MagicRule.Decide(Cast()));
    }

    [Fact]
    public void NotASpell_ComesFirst()
    {
        Assert.Equal(MagicDecision.NotASpell, MagicRule.Decide(Cast(bySpell: false, adrenalineOn: false, ready: false)));
    }

    [Fact]
    public void AdrenalineOff_BeforeTheMagicToggle()
    {
        Assert.Equal(MagicDecision.AdrenalineOff, MagicRule.Decide(Cast(adrenalineOn: false, magicOn: false)));
    }

    [Fact]
    public void MagicOff()
    {
        Assert.Equal(MagicDecision.MagicOff, MagicRule.Decide(Cast(magicOn: false, element: MagicElement.None)));
    }

    [Fact]
    public void UnknownElement_BeforeItsToggle()
    {
        Assert.Equal(MagicDecision.UnknownElement, MagicRule.Decide(Cast(element: MagicElement.None, elementOn: false)));
    }

    [Fact]
    public void ElementOff()
    {
        Assert.Equal(MagicDecision.ElementOff, MagicRule.Decide(Cast(elementOn: false, ready: false)));
    }

    [Fact]
    public void TargetIsPlayer()
    {
        Assert.Equal(MagicDecision.TargetIsPlayer, MagicRule.Decide(Cast(targetIsPlayer: true, ready: false)));
    }

    [Fact]
    public void TargetDead()
    {
        Assert.Equal(MagicDecision.TargetDead, MagicRule.Decide(Cast(targetDead: true, ready: false)));
    }

    [Fact]
    public void NotReady_IsTheLastRefusal()
    {
        Assert.Equal(MagicDecision.NotReady, MagicRule.Decide(Cast(ready: false)));
    }

    [Theory]
    [InlineData(MagicDecision.Magic, true)]
    [InlineData(MagicDecision.NotReady, false)]
    [InlineData(MagicDecision.ElementOff, false)]
    [InlineData(MagicDecision.NotASpell, false)]
    public void IsMagic(MagicDecision decision, bool expected)
    {
        Assert.Equal(expected, MagicRule.IsMagic(decision));
    }

    [Fact]
    public void Around_LivingEnemiesInReach_NearestFirst()
    {
        var others = new[]
        {
            new MagicTarget(1, 4.0f, alive: true, isPlayer: false),
            new MagicTarget(2, 1.5f, alive: true, isPlayer: false),
            new MagicTarget(3, 2.0f, alive: false, isPlayer: false),
            new MagicTarget(4, 0.5f, alive: true, isPlayer: true),
            new MagicTarget(5, 5.01f, alive: true, isPlayer: false),
            new MagicTarget(6, 5.0f, alive: true, isPlayer: false),
        };

        Assert.Equal(new[] { 2, 1, 6 }, MagicRule.Around(others, 5f));
    }

    [Fact]
    public void Around_RadiusZero_Nobody()
    {
        Assert.Empty(MagicRule.Around(new[] { new MagicTarget(1, 0f, true, false) }, 0f));
        Assert.Empty(MagicRule.Around(null, 5f));
    }

    [Fact]
    public void Settings_Defaults_AreAllOn_WithTheirSliders()
    {
        Assert.True(AdrenalineMagicSettings.DefaultEnabled);
        Assert.True(AdrenalineMagicSettings.DefaultFire);
        Assert.True(AdrenalineMagicSettings.DefaultLightning);
        Assert.True(AdrenalineMagicSettings.DefaultGravity);
        Assert.Equal(3f, AdrenalineMagicSettings.FireRadius.Default);
        Assert.Equal(50f, AdrenalineMagicSettings.FireSpreadHeat.Default);
        Assert.Equal(5f, AdrenalineMagicSettings.LightningRadius.Default);
        Assert.Equal(4f, AdrenalineMagicSettings.LightningShock.Default);
        Assert.Equal(4f, AdrenalineMagicSettings.GravityRadius.Default);
        Assert.Equal(12f, AdrenalineMagicSettings.GravityForce.Default);
        Assert.All(AdrenalineMagicSettings.Sliders, s => Assert.InRange(s.Default, s.Min, s.Max));
        Assert.Equal(6, AdrenalineMagicSettings.Sliders.Count());
    }
}
