using System.Collections.Generic;
using System.Linq;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class SpeedHistoryTests
{
    [Fact]
    public void Empty_PeakIsZero()
    {
        Assert.Equal(0f, new SpeedHistory(1f).Peak(10f, 0.25f));
    }

    [Fact]
    public void Peak_IsTheHighestSampleInsideTheWindow_BoundaryIncluded()
    {
        var h = new SpeedHistory(1f);
        h.Add(9.70f, 7f);   // outside a 0.25 s window at t=10
        h.Add(9.75f, 5f);   // exactly on the boundary
        h.Add(9.90f, 3f);
        h.Add(10.00f, 1f);

        Assert.Equal(5f, h.Peak(10f, 0.25f));
    }

    [Fact]
    public void ZeroWindow_UsesTheLatestSampleOnly()
    {
        var h = new SpeedHistory(1f);
        h.Add(9.9f, 6f);
        h.Add(10f, 2f);

        Assert.Equal(2f, h.Peak(10f, 0f));
    }

    [Fact]
    public void ZeroWindow_WithOnlyOlderSamples_StillUsesTheLatest()
    {
        var h = new SpeedHistory(1f);
        h.Add(9.5f, 4f);

        Assert.Equal(4f, h.Peak(10f, 0f));
    }

    [Fact]
    public void SamplesOlderThanTheRetention_AreDropped()
    {
        var h = new SpeedHistory(0.5f);
        h.Add(0f, 9f);
        h.Add(1f, 2f);

        Assert.Equal(1, h.Count);
        Assert.Equal(2f, h.Peak(1f, 5f));
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var h = new SpeedHistory(1f);
        h.Add(1f, 5f);
        h.Clear();

        Assert.Equal(0, h.Count);
        Assert.Equal(0f, h.Peak(1f, 1f));
    }
}

public class ChargeStaggerRuleTests
{
    private static ChargeHit Hit(
        bool enabled = true,
        bool finalPhase = true,
        HitSource source = HitSource.HeldWeapon,
        bool rangedCounts = true,
        bool targetIsPlayer = false,
        bool targetKilled = false,
        bool alreadyStaggered = false,
        bool secondKnocksDown = true,
        float peakSpeed = 6f,
        float threshold = 4.5f,
        float impactSpeed = 5f,
        float minImpactSpeed = 1f,
        float roll = 0f,
        float chance = 1f) =>
        new ChargeHit(enabled, finalPhase, source, rangedCounts, targetIsPlayer, targetKilled, alreadyStaggered, secondKnocksDown,
            peakSpeed, threshold, impactSpeed, minImpactSpeed, roll, chance);

    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void ChargeHit_FromEveryPlayerSource_Staggers(HitSource source)
    {
        Assert.Equal(ChargeDecision.Stagger, ChargeStaggerRule.Decide(Hit(source: source)));
    }

    [Theory]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void RangedHits_DoNotCount_WhenSwitchedOff(HitSource source)
    {
        Assert.Equal(ChargeDecision.RangedNotCounted, ChargeStaggerRule.Decide(Hit(source: source, rangedCounts: false)));
    }

    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    public void MeleeHits_IgnoreTheRangedSwitch(HitSource source)
    {
        Assert.Equal(ChargeDecision.Stagger, ChargeStaggerRule.Decide(Hit(source: source, rangedCounts: false)));
    }

    [Fact]
    public void BothThresholds_AreInclusive()
    {
        Assert.Equal(ChargeDecision.Stagger, ChargeStaggerRule.Decide(Hit(peakSpeed: 4.5f, impactSpeed: 1f)));
    }

    [Fact]
    public void SecondChargeHit_OnAStaggeredNpc_KnocksItDown()
    {
        Assert.Equal(ChargeDecision.KnockDown, ChargeStaggerRule.Decide(Hit(alreadyStaggered: true)));
    }

    [Fact]
    public void SecondChargeHit_WithTheKnockDownSwitchOff_DoesNothing()
    {
        Assert.Equal(ChargeDecision.AlreadyStaggered, ChargeStaggerRule.Decide(Hit(alreadyStaggered: true, secondKnocksDown: false)));
    }

    [Fact]
    public void SecondHit_ThatIsNoChargeHit_DoesNotKnockDown()
    {
        Assert.Equal(ChargeDecision.TooSlow, ChargeStaggerRule.Decide(Hit(alreadyStaggered: true, peakSpeed: 1f)));
    }

    [Theory]
    [InlineData(0.00f, 0.5f, ChargeDecision.Stagger)]
    [InlineData(0.49f, 0.5f, ChargeDecision.Stagger)]
    [InlineData(0.50f, 0.5f, ChargeDecision.ChanceFailed)]
    [InlineData(0.99f, 1.0f, ChargeDecision.Stagger)]
    [InlineData(0.00f, 0.0f, ChargeDecision.ChanceFailed)]
    public void Chance_RollBelowTheChanceSucceeds(float roll, float chance, ChargeDecision expected)
    {
        Assert.Equal(expected, ChargeStaggerRule.Decide(Hit(roll: roll, chance: chance)));
    }

    [Fact]
    public void Chance_AlsoGatesTheKnockDown()
    {
        Assert.Equal(ChargeDecision.ChanceFailed, ChargeStaggerRule.Decide(Hit(alreadyStaggered: true, roll: 0.9f, chance: 0.5f)));
    }

    [Theory]
    [InlineData(false, true, HitSource.HeldWeapon, false, false, 6f, 5f, ChargeDecision.Disabled)]
    [InlineData(true, false, HitSource.HeldWeapon, false, false, 6f, 5f, ChargeDecision.NotFinalPhase)]
    [InlineData(true, true, HitSource.NotPlayer, false, false, 6f, 5f, ChargeDecision.NotByPlayer)]
    [InlineData(true, true, HitSource.HeldWeapon, true, false, 6f, 5f, ChargeDecision.TargetIsPlayer)]
    [InlineData(true, true, HitSource.HeldWeapon, false, true, 6f, 5f, ChargeDecision.TargetDead)]
    [InlineData(true, true, HitSource.HeldWeapon, false, false, 4.49f, 5f, ChargeDecision.TooSlow)]
    [InlineData(true, true, HitSource.HeldWeapon, false, false, 6f, 0.99f, ChargeDecision.SwingTooWeak)]
    public void EveryRefusal_HasItsOwnReason(bool enabled, bool finalPhase, HitSource source, bool targetIsPlayer, bool killed, float speed, float impact, ChargeDecision expected)
    {
        Assert.Equal(expected, ChargeStaggerRule.Decide(Hit(enabled, finalPhase, source, targetIsPlayer: targetIsPlayer, targetKilled: killed, peakSpeed: speed, impactSpeed: impact)));
    }

    [Fact]
    public void Disabled_WinsOverEverythingElse()
    {
        Assert.Equal(ChargeDecision.Disabled, ChargeStaggerRule.Decide(Hit(enabled: false, finalPhase: false, source: HitSource.NotPlayer, peakSpeed: 0f)));
    }

    [Theory]
    [InlineData(ChargeDecision.Stagger, false)]
    [InlineData(ChargeDecision.KnockDown, false)]
    [InlineData(ChargeDecision.TooSlow, false)]
    [InlineData(ChargeDecision.SwingTooWeak, false)]
    [InlineData(ChargeDecision.ChanceFailed, false)]
    [InlineData(ChargeDecision.AlreadyStaggered, false)]
    [InlineData(ChargeDecision.RangedNotCounted, false)]
    [InlineData(ChargeDecision.Disabled, true)]
    [InlineData(ChargeDecision.NotFinalPhase, true)]
    [InlineData(ChargeDecision.NotByPlayer, true)]
    [InlineData(ChargeDecision.TargetIsPlayer, true)]
    [InlineData(ChargeDecision.TargetDead, true)]
    public void OnlyDecisionsAboutAPlayerHitOnALivingNpc_AreWorthLogging(ChargeDecision decision, bool quiet)
    {
        Assert.Equal(quiet, ChargeStaggerRule.IsQuiet(decision));
    }
}

public class ChargePresetTests
{
    [Fact]
    public void Defaults_AreTheGameyPreset()
    {
        Assert.Equal(ChargePreset.Gamey, ChargePresets.Parse(ChargeStaggerSettings.DefaultPreset));
        Assert.Equal(ChargePreset.Gamey, ChargePresets.Detect(ChargeStaggerSettings.Defaults));
    }

    [Theory]
    [InlineData(ChargePreset.Gamey)]
    [InlineData(ChargePreset.Realistic)]
    public void EachPreset_IsRecognisedFromItsOwnValues(ChargePreset preset)
    {
        Assert.Equal(preset, ChargePresets.Detect(ChargePresets.ValuesOf(preset)));
    }

    [Fact]
    public void Realistic_IsHarderThanGamey()
    {
        var g = ChargePresets.ValuesOf(ChargePreset.Gamey);
        var r = ChargePresets.ValuesOf(ChargePreset.Realistic);

        Assert.True(r.SpeedThreshold > g.SpeedThreshold);
        Assert.True(r.SpeedWindow < g.SpeedWindow);
        Assert.True(r.MinSwingSpeed > g.MinSwingSpeed);
        Assert.True(r.ChancePercent <= g.ChancePercent);
        Assert.True(r.Duration < g.Duration);
    }

    [Fact]
    public void AnyChangedValue_MakesItCustom()
    {
        var g = ChargePresets.ValuesOf(ChargePreset.Gamey);

        Assert.Equal(ChargePreset.Custom, ChargePresets.Detect(g.With(speedThreshold: g.SpeedThreshold + 0.25f)));
        Assert.Equal(ChargePreset.Custom, ChargePresets.Detect(g.With(strength: "Light")));
        Assert.Equal(ChargePreset.Custom, ChargePresets.Detect(g.With(chancePercent: 50)));
    }

    [Fact]
    public void Names_AndParse_RoundTrip_UnknownIsCustom()
    {
        Assert.Equal(new[] { "Gamey", "Realistic", "Custom" }, ChargePresets.Names);
        foreach (var name in ChargePresets.Names)
        {
            Assert.Equal(name, ChargePresets.NameOf(ChargePresets.Parse(name)));
        }
        Assert.Equal(ChargePreset.Custom, ChargePresets.Parse("Silly"));
        Assert.Equal(ChargePreset.Custom, ChargePresets.Parse(null));
    }

    [Fact]
    public void Custom_HasNoValuesOfItsOwn()
    {
        Assert.Null(ChargePresets.ValuesOf(ChargePreset.Custom));
    }

    [Theory]
    [InlineData(ChargePreset.Gamey)]
    [InlineData(ChargePreset.Realistic)]
    public void EveryPresetValue_IsSelectableInTheMenu(ChargePreset preset)
    {
        var v = ChargePresets.ValuesOf(preset);

        Assert.True(SliderValues.IndexOf(ChargeStaggerSettings.SpeedThreshold.Values(), v.SpeedThreshold) >= 0);
        Assert.True(SliderValues.IndexOf(ChargeStaggerSettings.SpeedWindow.Values(), v.SpeedWindow) >= 0);
        Assert.True(SliderValues.IndexOf(ChargeStaggerSettings.MinSwingSpeed.Values(), v.MinSwingSpeed) >= 0);
        Assert.True(SliderValues.IndexOf(ChargeStaggerSettings.Duration.Values(), v.Duration) >= 0);
        Assert.Contains(v.ChancePercent, ChargeStaggerSettings.ChanceValues());
        Assert.Contains(v.Strength, StaggerStrengths.Names);
    }
}

public class StaggerWindowsTests
{
    [Fact]
    public void MarkedTarget_IsStaggeredUntilTheWindowEnds()
    {
        var w = new StaggerWindows<int>();
        w.Mark(7, 10f, 2f);

        Assert.True(w.IsStaggered(7, 10f));
        Assert.True(w.IsStaggered(7, 11.99f));
        Assert.False(w.IsStaggered(7, 12f));
        Assert.False(w.IsStaggered(8, 10f));
    }

    [Fact]
    public void Remarking_ExtendsButNeverShortens()
    {
        var w = new StaggerWindows<int>();
        w.Mark(1, 0f, 5f);
        w.Mark(1, 1f, 1f);

        Assert.True(w.IsStaggered(1, 4.9f));

        w.Mark(1, 4f, 3f);
        Assert.True(w.IsStaggered(1, 6.9f));
    }

    [Fact]
    public void TakeExpired_ReturnsEachEndedTargetOnce()
    {
        var w = new StaggerWindows<int>();
        w.Mark(1, 0f, 1f);
        w.Mark(2, 0f, 3f);

        Assert.Equal(new[] { 1 }, w.TakeExpired(1f));
        Assert.Empty(w.TakeExpired(1.5f));
        Assert.Equal(new[] { 2 }, w.TakeExpired(3f));
        Assert.Equal(0, w.Count);
    }

    [Fact]
    public void Remove_ForgetsOneTargetOnly()
    {
        var w = new StaggerWindows<int>();
        w.Mark(1, 0f, 5f);
        w.Mark(2, 0f, 5f);

        Assert.True(w.Remove(1));
        Assert.False(w.Remove(1));
        Assert.False(w.IsStaggered(1, 1f));
        Assert.True(w.IsStaggered(2, 1f));
    }

    [Fact]
    public void TakeAll_EmptiesTheTracker()
    {
        var w = new StaggerWindows<int>();
        w.Mark(1, 0f, 1f);
        w.Mark(2, 0f, 9f);

        Assert.Equal(new[] { 1, 2 }, w.TakeAll().OrderBy(x => x));
        Assert.Equal(0, w.Count);
        Assert.False(w.IsStaggered(2, 0f));
    }
}

public class SliderValuesTests
{
    [Fact]
    public void Create_MatchesTheGamesFormula_StartPlusIndexTimesStep()
    {
        // ThunderRoad ModOptionFloat.CreateArray: count = CeilToInt((end - start) / step + 1), value = start + i * step.
        Assert.Equal(new[] { 1f, 1.5f, 2f }, SliderValues.Create(1f, 2f, 0.5f));
    }

    [Fact]
    public void IndexOf_IsExact_LikeTheGamesDefaultLookup()
    {
        var values = SliderValues.Create(0f, 1f, 0.25f);

        Assert.Equal(2, SliderValues.IndexOf(values, 0.5f));
        Assert.Equal(-1, SliderValues.IndexOf(values, 0.3f));
    }

    public static IEnumerable<object[]> Sliders() =>
        ChargeStaggerSettings.Sliders.Select(s => new object[] { s.Name });

    [Theory]
    [MemberData(nameof(Sliders))]
    public void EveryChargeStaggerDefault_IsExactlyOneOfItsSliderValues(string name)
    {
        // Otherwise the game falls back to the first value (ModOption.ReadModOptionDefaultValue uses Equals).
        var slider = ChargeStaggerSettings.Sliders.Single(s => s.Name == name);

        Assert.True(SliderValues.IndexOf(SliderValues.Create(slider.Min, slider.Max, slider.Step), slider.Default) >= 0);
    }
}

public class StaggerStrengthsTests
{
    [Fact]
    public void MenuNames_RoundTrip()
    {
        foreach (var name in StaggerStrengths.Names)
        {
            Assert.Equal(name, StaggerStrengths.NameOf(StaggerStrengths.Parse(name)));
        }
    }

    [Fact]
    public void Names_AreOrderedWeakToStrong()
    {
        Assert.Equal(new[] { "Light", "Medium", "Full", "Knock Down" }, StaggerStrengths.Names);
    }

    [Fact]
    public void Default_IsAMenuName()
    {
        Assert.Contains(ChargeStaggerSettings.DefaultStrength, StaggerStrengths.Names);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Mighty")]
    public void UnknownNames_FallBackToTheDefault(string name)
    {
        Assert.Equal(StaggerStrengths.Parse(ChargeStaggerSettings.DefaultStrength), StaggerStrengths.Parse(name));
    }

    [Fact]
    public void OnlyKnockDown_HoldsTheNpcOnTheGround()
    {
        Assert.True(StaggerStrengths.KnocksDown(StaggerStrength.KnockDown));
        Assert.False(StaggerStrengths.KnocksDown(StaggerStrength.Full));
    }
}
