using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class AdrenalineMeterTests
{
    private const float Max = 100f;
    private const float Window = 6f;

    private static AdrenalineMeter Filled(float now = 0f)
    {
        var m = new AdrenalineMeter();
        m.Gain(Max, now, Max, Window);
        return m;
    }

    [Fact]
    public void Gains_AddUp_WhileFilling()
    {
        var m = new AdrenalineMeter();

        Assert.Equal(AdrenalineEvent.Gained, m.Gain(5f, 0f, Max, Window));
        Assert.Equal(AdrenalineEvent.Gained, m.Gain(10f, 1f, Max, Window));
        Assert.Equal(15f, m.Value);
        Assert.Equal(AdrenalineState.Filling, m.State);
    }

    [Fact]
    public void ReachingTheMax_OpensTheWindow_AndCapsTheValue()
    {
        var m = new AdrenalineMeter();
        m.Gain(95f, 0f, Max, Window);

        Assert.Equal(AdrenalineEvent.Filled, m.Gain(10f, 2f, Max, Window));
        Assert.Equal(Max, m.Value);
        Assert.Equal(AdrenalineState.Ready, m.State);
        Assert.Equal(8f, m.WindowEndsAt);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-3f)]
    public void ZeroOrNegativeGains_DoNothing(float amount)
    {
        var m = new AdrenalineMeter();

        Assert.Equal(AdrenalineEvent.None, m.Gain(amount, 0f, Max, Window));
        Assert.Equal(0f, m.Value);
    }

    [Fact]
    public void GainsWhileReady_DoNotMoveTheWindow()
    {
        var m = Filled();

        Assert.Equal(AdrenalineEvent.None, m.Gain(5f, 3f, Max, Window));
        Assert.Equal(Window, m.WindowEndsAt);
    }

    [Fact]
    public void AnUnusedWindow_EmptiesTheMeter()
    {
        var m = Filled();

        Assert.Equal(AdrenalineEvent.None, m.Tick(5.9f, 0.1f, 0f, 0f));
        Assert.Equal(AdrenalineEvent.WindowExpired, m.Tick(6f, 0.1f, 0f, 0f));
        Assert.Equal(0f, m.Value);
        Assert.Equal(AdrenalineState.Filling, m.State);
    }

    [Fact]
    public void Decay_DrainsWhileFilling_AfterTheDelay()
    {
        var m = new AdrenalineMeter();
        m.Gain(50f, 0f, Max, Window);

        Assert.Equal(AdrenalineEvent.None, m.Tick(1f, 1f, 2f, 3f));
        Assert.Equal(50f, m.Value);
        Assert.Equal(AdrenalineEvent.None, m.Tick(3f, 1f, 2f, 3f));
        Assert.Equal(48f, m.Value);
    }

    [Fact]
    public void Decay_WithoutDelay_StartsAtOnce()
    {
        var m = new AdrenalineMeter();
        m.Gain(10f, 0f, Max, Window);

        m.Tick(0.5f, 0.5f, 2f, 0f);

        Assert.Equal(9f, m.Value);
    }

    [Fact]
    public void ANewGain_RestartsTheDelay()
    {
        var m = new AdrenalineMeter();
        m.Gain(10f, 0f, Max, Window);
        m.Gain(10f, 4f, Max, Window);

        m.Tick(5f, 1f, 2f, 3f);

        Assert.Equal(20f, m.Value);
    }

    [Fact]
    public void DrainingToZero_IsReportedOnce()
    {
        var m = new AdrenalineMeter();
        m.Gain(3f, 0f, Max, Window);

        Assert.Equal(AdrenalineEvent.Drained, m.Tick(1f, 1f, 5f, 0f));
        Assert.Equal(0f, m.Value);
        Assert.Equal(AdrenalineEvent.None, m.Tick(2f, 1f, 5f, 0f));
    }

    [Fact]
    public void ZeroDecay_NeverDrains()
    {
        var m = new AdrenalineMeter();
        m.Gain(10f, 0f, Max, Window);

        m.Tick(100f, 100f, 0f, 0f);

        Assert.Equal(10f, m.Value);
    }

    [Fact]
    public void AReadyMeter_DoesNotDecay()
    {
        var m = Filled();

        m.Tick(1f, 1f, 50f, 0f);

        Assert.Equal(Max, m.Value);
        Assert.Equal(AdrenalineState.Ready, m.State);
    }

    [Fact]
    public void Consume_UsesAReadyMeter_Once()
    {
        var m = Filled();

        Assert.True(m.Consume());
        Assert.Equal(0f, m.Value);
        Assert.Equal(AdrenalineState.Filling, m.State);
        Assert.False(m.Consume());
    }

    [Fact]
    public void Consume_OnAFillingMeter_DoesNothing()
    {
        var m = new AdrenalineMeter();
        m.Gain(40f, 0f, Max, Window);

        Assert.False(m.Consume());
        Assert.Equal(40f, m.Value);
    }

    [Fact]
    public void Reset_EmptiesEverything()
    {
        var m = Filled();

        m.Reset();

        Assert.Equal(0f, m.Value);
        Assert.Equal(AdrenalineState.Filling, m.State);
    }

    [Fact]
    public void ALowerMax_CapsAFillingMeter_WithoutFillingIt()
    {
        var m = new AdrenalineMeter();
        m.Gain(80f, 0f, Max, Window);

        m.Cap(50f);

        Assert.Equal(50f, m.Value);
        Assert.Equal(AdrenalineState.Filling, m.State);
        Assert.Equal(AdrenalineEvent.Filled, m.Gain(1f, 1f, 50f, Window));
    }
}

public class AdrenalineSourcesTests
{
    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void APlayerHitOnAnNpc_IsAHit(HitSource source)
    {
        Assert.Equal(AdrenalineSource.Hit, AdrenalineSources.FromHit(true, source, false, 1f));
    }

    [Fact]
    public void AnNpcHitOnThePlayer_WithDamage_IsDamageTaken()
    {
        Assert.Equal(AdrenalineSource.DamageTaken, AdrenalineSources.FromHit(true, HitSource.NotPlayer, true, 3f));
    }

    [Fact]
    public void AHitWithoutDamage_OnThePlayer_IsNothing()
    {
        Assert.Null(AdrenalineSources.FromHit(true, HitSource.NotPlayer, true, 0f));
    }

    [Fact]
    public void ThePlayerHittingThemself_IsNothing()
    {
        Assert.Null(AdrenalineSources.FromHit(true, HitSource.HeldWeapon, true, 5f));
    }

    [Fact]
    public void NpcVsNpc_IsNothing()
    {
        Assert.Null(AdrenalineSources.FromHit(true, HitSource.NotPlayer, false, 5f));
    }

    [Fact]
    public void OnlyTheEndOfAHit_Counts()
    {
        Assert.Null(AdrenalineSources.FromHit(false, HitSource.HeldWeapon, false, 1f));
        Assert.Null(AdrenalineSources.FromHit(false, HitSource.NotPlayer, true, 1f));
    }

    [Fact]
    public void ThePlayerParryingAnNpc_IsABlockOrParry()
    {
        Assert.Equal(AdrenalineSource.BlockParry, AdrenalineSources.FromParry(parriedIsPlayer: false, parryingIsPlayer: true));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void OtherParries_AreNothing(bool parriedIsPlayer, bool parryingIsPlayer)
    {
        Assert.Null(AdrenalineSources.FromParry(parriedIsPlayer, parryingIsPlayer));
    }
}

public class AdrenalineSettingsTests
{
    [Fact]
    public void GainDefaults_MatchTheSpec()
    {
        Assert.Equal(5, AdrenalineSettings.DefaultGainHit);
        Assert.Equal(10, AdrenalineSettings.DefaultGainBlockParry);
        Assert.Equal(5, AdrenalineSettings.DefaultGainDamageTaken);
        Assert.Equal(15, AdrenalineSettings.DefaultGainChargeStagger);
        Assert.Equal(15, AdrenalineSettings.DefaultGainDisarm);
    }

    [Fact]
    public void MeterDefaults_MatchTheSpec()
    {
        Assert.Equal(100f, AdrenalineSettings.MeterMax.Default);
        Assert.Equal(6f, AdrenalineSettings.WindowDuration.Default);
        Assert.Equal(2f, AdrenalineSettings.DecayPerSecond.Default);
        Assert.Equal(0f, AdrenalineSettings.DecayDelay.Default);
    }

    [Fact]
    public void EverySliderDefault_IsOneOfItsValues()
    {
        foreach (var s in AdrenalineSettings.Sliders)
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name);
        }
        foreach (var d in new[]
                 {
                     AdrenalineSettings.DefaultGainHit, AdrenalineSettings.DefaultGainBlockParry, AdrenalineSettings.DefaultGainDamageTaken,
                     AdrenalineSettings.DefaultGainChargeStagger, AdrenalineSettings.DefaultGainDisarm,
                 })
        {
            Assert.Contains(d, AdrenalineSettings.GainValues());
        }
    }

    [Theory]
    [InlineData(AdrenalineSource.Hit, 1)]
    [InlineData(AdrenalineSource.BlockParry, 2)]
    [InlineData(AdrenalineSource.DamageTaken, 3)]
    [InlineData(AdrenalineSource.ChargeStagger, 4)]
    [InlineData(AdrenalineSource.Disarm, 5)]
    public void EachSource_UsesItsOwnGain(AdrenalineSource source, int expected)
    {
        var gains = new AdrenalineGains(1, 2, 3, 4, 5);

        Assert.Equal(expected, gains.For(source));
    }
}

public class AdrenalineLoseTests
{
    [Fact]
    public void Lose_TakesPointsOff_NotBelowZero()
    {
        var m = new AdrenalineMeter();
        m.Gain(8f, 0f, 100f, 6f);

        Assert.Equal(AdrenalineEvent.Lost, m.Lose(5f));
        Assert.Equal(3f, m.Value);
        Assert.Equal(AdrenalineEvent.Lost, m.Lose(5f));
        Assert.Equal(0f, m.Value);
        Assert.Equal(AdrenalineEvent.None, m.Lose(5f));
    }

    [Fact]
    public void Lose_KeepsAFullMetersWindow()
    {
        var m = new AdrenalineMeter();
        m.Gain(100f, 0f, 100f, 6f);

        Assert.Equal(AdrenalineEvent.None, m.Lose(5f));
        Assert.Equal(AdrenalineState.Ready, m.State);
    }

    [Fact]
    public void Lose_ZeroDoesNothing()
    {
        var m = new AdrenalineMeter();
        m.Gain(8f, 0f, 100f, 6f);

        Assert.Equal(AdrenalineEvent.None, m.Lose(0f));
    }

    [Fact]
    public void NewDefaults_DamageLowers_DecayOff()
    {
        Assert.True(AdrenalineSettings.DefaultDamageTakenLowers);
        Assert.False(AdrenalineSettings.DefaultDecayEnabled);
    }
}

public class CriticalRuleTests
{
    private static CriticalHit Hit(
        bool enabled = true, bool ready = true, HitSource source = HitSource.HeldWeapon, bool rangedCounts = false,
        bool targetIsPlayer = false, bool targetDead = false, bool needsHard = true, float impact = 5f, float minSwing = 3f) =>
        new CriticalHit(enabled, ready, source, rangedCounts, targetIsPlayer, targetDead, needsHard, impact, minSwing);

    [Theory]
    [InlineData(HitSource.HeldWeapon)]
    [InlineData(HitSource.PlayerBody)]
    public void AHardMeleeHit_WithAFullMeter_IsTheCritical(HitSource source)
    {
        Assert.Equal(CriticalDecision.Critical, CriticalRule.Decide(Hit(source: source)));
    }

    [Theory]
    [InlineData(HitSource.PlayerThrown)]
    [InlineData(HitSource.PlayerSpell)]
    public void ThrownAndSpells_OnlyWhenSwitchedOn(HitSource source)
    {
        Assert.Equal(CriticalDecision.Ranged, CriticalRule.Decide(Hit(source: source)));
        Assert.Equal(CriticalDecision.Critical, CriticalRule.Decide(Hit(source: source, rangedCounts: true)));
    }

    [Fact]
    public void ALightHit_IsNotTheCritical_WhileAHardSwingIsNeeded()
    {
        Assert.Equal(CriticalDecision.TooLight, CriticalRule.Decide(Hit(impact: 2.9f)));
        Assert.Equal(CriticalDecision.Critical, CriticalRule.Decide(Hit(impact: 3f)));
        Assert.Equal(CriticalDecision.Critical, CriticalRule.Decide(Hit(impact: 0.5f, needsHard: false)));
    }

    [Fact]
    public void TheRefusals_ComeInOrder()
    {
        Assert.Equal(CriticalDecision.Disabled, CriticalRule.Decide(Hit(enabled: false, source: HitSource.NotPlayer)));
        Assert.Equal(CriticalDecision.NotByPlayer, CriticalRule.Decide(Hit(source: HitSource.NotPlayer, targetIsPlayer: true)));
        Assert.Equal(CriticalDecision.TargetIsPlayer, CriticalRule.Decide(Hit(targetIsPlayer: true, targetDead: true)));
        Assert.Equal(CriticalDecision.TargetDead, CriticalRule.Decide(Hit(targetDead: true, ready: false)));
        Assert.Equal(CriticalDecision.NotReady, CriticalRule.Decide(Hit(ready: false, source: HitSource.PlayerThrown)));
        Assert.Equal(CriticalDecision.Ranged, CriticalRule.Decide(Hit(source: HitSource.PlayerThrown, impact: 0f)));
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, true, false)]
    public void AChestStab_IsATorsoHitThatWentIn(bool torso, bool penetrating, bool pierce, bool expected)
    {
        Assert.Equal(expected, CriticalRule.IsChestStab(torso, penetrating, pierce));
    }
}

public class KneelClipsTests
{
    private static readonly string[] StabDeaths =
    {
        "Bas.Animation.Death_Backward", "Bas.Animation.Death_KneelBackward", "Bas.Animation.Death_KneelMid",
        "Bas.Animation.Death_2", "Bas.Animation.Death1", "Bas.Animation.Death2", "Bas.Animation.Death_KneelForward",
        "Bas.Animation.Death_KneelForward2", "Bas.Animation.Death_KneelForward3", "Bas.Animation.Death_back",
        "Bas.Animation.Death_Left", "Bas.Animation.Death.SimpleRight",
    };

    [Fact]
    public void AStabFromTheFront_KneelsBackwardOrMid()
    {
        Assert.Equal(
            new[] { "Bas.Animation.Death_KneelBackward", "Bas.Animation.Death_KneelMid" },
            KneelClips.For(DeathDirection.Backward, StabDeaths));
    }

    [Fact]
    public void AStabFromBehind_KneelsForward()
    {
        Assert.Equal(
            new[] { "Bas.Animation.Death_KneelForward", "Bas.Animation.Death_KneelForward2", "Bas.Animation.Death_KneelForward3" },
            KneelClips.For(DeathDirection.Forward, StabDeaths));
    }

    [Theory]
    [InlineData(DeathDirection.Left)]
    [InlineData(DeathDirection.Right)]
    public void FromTheSide_KneelsMid(DeathDirection direction)
    {
        Assert.Equal(new[] { "Bas.Animation.Death_KneelMid" }, KneelClips.For(direction, StabDeaths));
    }

    [Fact]
    public void WithoutAFittingClip_AnyKneelingOneIsUsed()
    {
        Assert.Equal(new[] { "X.KneelForward" }, KneelClips.For(DeathDirection.Left, new[] { "X.KneelForward", "X.Death" }));
    }

    [Fact]
    public void WithoutAnyKneelingClip_NothingIsReturned()
    {
        Assert.Empty(KneelClips.For(DeathDirection.Backward, new[] { "X.Death" }));
        Assert.Empty(KneelClips.For(DeathDirection.Backward, null));
    }
}

public class CriticalTimeModeTests
{
    [Fact]
    public void HyperfocusBuffsThePlayer_SlowTimeDoesNot()
    {
        Assert.True(CriticalTimeModes.BuffsPlayer(CriticalTimeModes.Hyperfocus));
        Assert.False(CriticalTimeModes.BuffsPlayer(CriticalTimeModes.SlowTime));
        Assert.Equal(CriticalTimeModes.Hyperfocus, AdrenalineSettings.DefaultTimeMode);
    }

    [Fact]
    public void AtTheGamesHyperfocusScale_TheBuffsAreTheSkills()
    {
        var (joint, speed, drag) = CriticalTimeModes.Buffs(0.1f);

        Assert.Equal(10f, joint, 3);
        Assert.Equal(20f, speed, 3);
        Assert.Equal(10f, drag, 3);
    }

    [Fact]
    public void CriticalSliderDefaults_AreOnTheirSliders()
    {
        foreach (var s in AdrenalineSettings.CriticalSliders)
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name);
        }
    }

    [Fact]
    public void CriticalToggleDefaults_MatchTheSpec()
    {
        Assert.True(AdrenalineSettings.DefaultNeedsHardSwing);
        Assert.False(AdrenalineSettings.DefaultRangedCounts);
        Assert.True(AdrenalineSettings.DefaultKills);
        Assert.True(AdrenalineSettings.DefaultBattleCry);
        Assert.True(AdrenalineSettings.DefaultChestStabKneels);
    }
}
