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
