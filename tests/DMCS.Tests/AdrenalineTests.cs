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

    // Double fatality (user 2026-11-23, default off): the first critical leaves the meter full and opens a fresh window
    // for a second one; the second empties it.

    [Fact]
    public void DoubleFatality_FirstCriticalReopensTheWindow_SecondEmptiesIt()
    {
        var m = Filled();

        Assert.Equal(CriticalSpend.SecondReady, m.Spend(doubleFatality: true, now: 3f, windowSeconds: Window));
        Assert.Equal(AdrenalineState.Ready, m.State);
        Assert.Equal(9f, m.WindowEndsAt);
        Assert.Equal(CriticalSpend.Spent, m.Spend(doubleFatality: true, now: 4f, windowSeconds: Window));
        Assert.Equal(AdrenalineState.Filling, m.State);
        Assert.Equal(0f, m.Value);
        Assert.Equal(CriticalSpend.NotReady, m.Spend(true, 5f, Window));
    }

    [Fact]
    public void DoubleFatality_Off_OneCriticalEmptiesIt()
    {
        var m = Filled();

        Assert.Equal(CriticalSpend.Spent, m.Spend(doubleFatality: false, now: 1f, windowSeconds: Window));
        Assert.Equal(AdrenalineState.Filling, m.State);
    }

    [Fact]
    public void DoubleFatality_SecondWindowExpires_TheNextFillGivesTwoAgain()
    {
        var m = Filled();
        m.Spend(true, 1f, Window);

        Assert.Equal(AdrenalineEvent.WindowExpired, m.Tick(7.5f, 0.1f, 0f, 0f));
        m.Gain(Max, 8f, Max, Window);

        Assert.Equal(CriticalSpend.SecondReady, m.Spend(true, 9f, Window));
    }

    [Fact]
    public void DoubleFatality_PlainConsume_EndsBothAtOnce()
    {
        var m = Filled();
        m.Spend(true, 1f, Window);

        Assert.True(m.Consume());
        Assert.Equal(AdrenalineState.Filling, m.State);
        m.Gain(Max, 2f, Max, Window);
        Assert.Equal(CriticalSpend.SecondReady, m.Spend(true, 3f, Window));
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
        // Headset 2026-11-22: "6 s ist zu wenig". 12 s is a guess.
        Assert.Equal(12f, AdrenalineSettings.WindowDuration.Default);
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
                     AdrenalineSettings.DefaultGainDodge, AdrenalineSettings.DefaultGainCatch,
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
    [InlineData(AdrenalineSource.Dodge, 6)]
    [InlineData(AdrenalineSource.Catch, 7)]
    [InlineData(AdrenalineSource.ClashWin, 8)]
    public void EachSource_UsesItsOwnGain(AdrenalineSource source, int expected)
    {
        var gains = new AdrenalineGains(1, 2, 3, 4, 5, 6, 7, 8);

        Assert.Equal(expected, gains.For(source));
    }

    [Fact]
    public void ClashWin_DefaultsTo25()
    {
        Assert.Equal(25, new AdrenalineGains(1, 2, 3, 4, 5, 6, 7).For(AdrenalineSource.ClashWin));
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

    // User 2026-11-26 (Windows log of the day: a fist and a blunt hit took heads off): only a cutting edge cuts, as in
    // Dark Messiah, where the sword's fatality dismembers and the staff's does not.
    [Theory]
    [InlineData(true, true, true, true)]     // a slash with Critical Dismember on cuts
    [InlineData(false, true, true, false)]   // Critical Dismember off
    [InlineData(true, false, true, false)]   // no hit part known
    [InlineData(true, true, false, false)]   // blunt, pierce, a fist or a kick: no cut, the critical still kills
    public void OnlyASlashCutsALimbOff(bool dismember, bool hasPart, bool slash, bool expected)
    {
        Assert.Equal(expected, CriticalRule.Cuts(dismember, hasPart, slash));
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

public class FinishRuleTests
{
    [Fact]
    public void AKick_InTheWindow_SendsTheKneelingEnemyFlying()
    {
        Assert.Equal(FinishMove.Kick, FinishRule.Decide(true, HitSource.PlayerBody, true, false, false));
    }

    [Fact]
    public void ASlashOnTheNeckOrHead_InTheWindow_Beheads()
    {
        Assert.Equal(FinishMove.Behead, FinishRule.Decide(true, HitSource.HeldWeapon, false, true, true));
    }

    [Theory]
    [InlineData(false, HitSource.PlayerBody, true, false, false)]
    [InlineData(false, HitSource.HeldWeapon, false, true, true)]
    [InlineData(true, HitSource.NotPlayer, true, true, true)]
    [InlineData(true, HitSource.PlayerBody, false, false, false)]
    [InlineData(true, HitSource.HeldWeapon, true, false, false)]
    [InlineData(true, HitSource.HeldWeapon, false, true, false)]
    [InlineData(true, HitSource.HeldWeapon, false, false, true)]
    public void EverythingElse_DoesNothing(bool inWindow, HitSource source, bool foot, bool headOrNeck, bool slash)
    {
        Assert.Equal(FinishMove.None, FinishRule.Decide(inWindow, source, foot, headOrNeck, slash));
    }

    [Fact]
    public void ConfirmedDefaults()
    {
        Assert.Equal(3f, AdrenalineSettings.MinSwingSpeed.Default);
        Assert.Equal(5f, AdrenalineSettings.ExecutionWindow.Default);
    }
}

public class KneelTrackerTests
{
    private static KneelTracker<int> Open(float now = 0f)
    {
        var k = new KneelTracker<int>();
        k.PlayerHit(7, now, false);
        k.WindowOpened(now);
        return k;
    }

    [Fact]
    public void NoHitForTheSetTime_Kneels()
    {
        var k = Open();

        Assert.Equal(KneelReason.None, k.Check(1.9f, true, 2f, 2));
        Assert.Equal(KneelReason.NoHit, k.Check(2f, true, 2f, 2));
        Assert.Equal(7, k.LastHit);
    }

    [Fact]
    public void AHardHit_RestartsTheNoHitTime()
    {
        var k = Open();
        k.PlayerHit(7, 1.5f, false);

        Assert.Equal(KneelReason.None, k.Check(3f, true, 2f, 2));
        Assert.Equal(KneelReason.NoHit, k.Check(3.5f, true, 2f, 2));
    }

    [Fact]
    public void OnlyLightHits_Kneel()
    {
        var k = Open();
        k.PlayerHit(7, 0.5f, true);

        Assert.Equal(KneelReason.None, k.Check(0.6f, true, 2f, 2));
        k.PlayerHit(7, 0.7f, true);
        Assert.Equal(KneelReason.LightHits, k.Check(0.8f, true, 2f, 2));
    }

    [Fact]
    public void LightHits_DoNotRestartTheNoHitTime()
    {
        var k = Open();
        k.PlayerHit(7, 1.5f, true);

        Assert.Equal(KneelReason.NoHit, k.Check(2f, true, 2f, 5));
    }

    [Fact]
    public void TheEnemyHitLast_IsTheOneThatKneels()
    {
        var k = Open();
        k.PlayerHit(9, 0.5f, false);

        Assert.Equal(9, k.LastHit);
    }

    [Fact]
    public void NothingHappens_WhileTheWindowIsClosed_OrSwitchedOff()
    {
        var k = new KneelTracker<int>();
        k.PlayerHit(7, 0f, true);
        k.PlayerHit(7, 0f, true);
        Assert.Equal(KneelReason.None, k.Check(10f, true, 2f, 2));

        var open = Open();
        Assert.Equal(KneelReason.None, open.Check(10f, false, 2f, 2));
        open.WindowClosed();
        Assert.Equal(KneelReason.None, open.Check(10f, true, 2f, 2));
    }

    [Fact]
    public void LightHitsBeforeTheWindow_DoNotCount()
    {
        var k = new KneelTracker<int>();
        k.PlayerHit(7, 0f, true);
        k.WindowOpened(1f);
        k.PlayerHit(7, 1.1f, true);

        Assert.Equal(KneelReason.None, k.Check(1.2f, true, 2f, 2));
    }

    [Fact]
    public void Forget_DropsTheEnemy_OnlyIfItIsTheLastHit()
    {
        var k = Open();
        k.Forget(3);
        Assert.True(k.HasLastHit);
        k.Forget(7);
        Assert.False(k.HasLastHit);
    }

    [Theory]
    [InlineData(0.5f, 0f, KneelEnd.StandsUp)]
    [InlineData(0f, 0f, KneelEnd.StandsUp)]
    [InlineData(0.99f, 1f, KneelEnd.Dies)]
    [InlineData(0.3f, 0.5f, KneelEnd.Dies)]
    [InlineData(0.5f, 0.5f, KneelEnd.StandsUp)]
    public void AfterTheWindow_TheEnemyStandsUp_OrDiesWithTheChance(float roll, float chance, KneelEnd expected)
    {
        Assert.Equal(expected, KneelOutcome.AfterWindow(roll, chance));
    }

    [Fact]
    public void KneelDefaults_AsConfirmed()
    {
        Assert.True(AdrenalineSettings.DefaultKneelEnabled);
        Assert.Equal(2f, AdrenalineSettings.KneelAfterNoHit.Default);
        Assert.Equal(2, AdrenalineSettings.DefaultKneelAfterLightHits);
        Assert.Equal(0, AdrenalineSettings.DefaultKneelDeathChance);
        Assert.Equal(3f, AdrenalineSettings.KneelMaxDistance.Default);
    }

    // Headset 2026-11-22: "passiert auch bei weit entfernten Gegnern ... sollte nur bei nahen passieren".

    private static KneelCandidate Enemy(int id, float distance, bool alive = true, bool attacksPlayer = true) =>
        new KneelCandidate(id, distance, alive, attacksPlayer);

    [Fact]
    public void KneelTarget_TheEnemyHitLast_WhenNear()
    {
        var all = new[] { Enemy(1, 2.5f, attacksPlayer: false), Enemy(2, 1f) };

        Assert.Equal(1, KneelTargetRule.Choose(1, all, 3f));
    }

    [Fact]
    public void KneelTarget_TheEnemyHitLast_TooFar_TheNearestAttackerInstead()
    {
        var all = new[] { Enemy(1, 8f), Enemy(2, 2.9f), Enemy(3, 1.2f) };

        Assert.Equal(3, KneelTargetRule.Choose(1, all, 3f));
    }

    [Fact]
    public void KneelTarget_TheEnemyHitLast_Dead_TheNearestAttackerInstead()
    {
        var all = new[] { Enemy(1, 1f, alive: false), Enemy(2, 2f) };

        Assert.Equal(2, KneelTargetRule.Choose(1, all, 3f));
    }

    [Fact]
    public void KneelTarget_NoHitYet_TheNearestLivingAttackerInReach()
    {
        var all = new[] { Enemy(1, 2f, attacksPlayer: false), Enemy(2, 2.5f), Enemy(3, 0.5f, alive: false), Enemy(4, 3f) };

        Assert.Equal(2, KneelTargetRule.Choose(null, all, 3f));
    }

    [Fact]
    public void KneelTarget_NobodyNear_Nobody()
    {
        var all = new[] { Enemy(1, 8f), Enemy(2, 3.01f) };

        Assert.Null(KneelTargetRule.Choose(1, all, 3f));
        Assert.Null(KneelTargetRule.Choose(null, null, 3f));
    }

    [Fact]
    public void KneelTarget_TheEnemyHitLast_NotInTheList_TheNearestAttacker()
    {
        Assert.Equal(2, KneelTargetRule.Choose(9, new[] { Enemy(2, 1f) }, 3f));
    }
}
