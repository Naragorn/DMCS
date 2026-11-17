using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class DodgeTrackerTests
{
    private const float MinSpeed = 2f;

    private static DodgeTracker<int> Attacking(int attacker = 1, float at = 10f)
    {
        var t = new DodgeTracker<int>();
        t.AttackStarted(attacker, at);
        return t;
    }

    [Fact]
    public void FastPlayer_NoHit_NoParry_IsADodge()
    {
        var t = Attacking();

        var result = t.AttackEnded(1, peakSpeed: 2.5f, MinSpeed);

        Assert.Equal(DodgeResult.Dodged, result);
    }

    [Fact]
    public void SpeedExactlyAtTheThreshold_Counts()
    {
        Assert.Equal(DodgeResult.Dodged, Attacking().AttackEnded(1, MinSpeed, MinSpeed));
    }

    [Fact]
    public void SlowPlayer_IsNoDodge()
    {
        Assert.Equal(DodgeResult.TooSlow, Attacking().AttackEnded(1, 1.9f, MinSpeed));
    }

    [Fact]
    public void HitByThatAttacker_IsNoDodge()
    {
        var t = Attacking();
        t.PlayerHitBy(1);

        Assert.Equal(DodgeResult.Hit, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void HitByAnotherAttacker_KeepsThisAttackOpen()
    {
        var t = Attacking();
        t.AttackStarted(2, 10f);
        t.PlayerHitBy(2);

        Assert.Equal(DodgeResult.Dodged, t.AttackEnded(1, 5f, MinSpeed));
        Assert.Equal(DodgeResult.Hit, t.AttackEnded(2, 5f, MinSpeed));
    }

    [Fact]
    public void HitFromUnknownAttacker_VoidsEveryOpenAttack()
    {
        var t = Attacking();
        t.AttackStarted(2, 10f);
        t.PlayerHitByUnknown();

        Assert.Equal(DodgeResult.Hit, t.AttackEnded(1, 5f, MinSpeed));
        Assert.Equal(DodgeResult.Hit, t.AttackEnded(2, 5f, MinSpeed));
    }

    [Fact]
    public void Parried_IsNoDodge()
    {
        var t = Attacking();
        t.Parried(1);

        Assert.Equal(DodgeResult.Parried, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void EndWithoutAttackStage_IsNotTracked()
    {
        Assert.Equal(DodgeResult.NotTracked, new DodgeTracker<int>().AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void CancelledAttack_IsForgotten()
    {
        var t = Attacking();
        t.AttackCancelled(1);

        Assert.Equal(DodgeResult.NotTracked, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void AnAttackEndsOnce()
    {
        var t = Attacking();
        t.AttackEnded(1, 5f, MinSpeed);

        Assert.Equal(DodgeResult.NotTracked, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void ANewAttack_StartsClean_AfterAHitOnTheLastOne()
    {
        var t = Attacking();
        t.PlayerHitBy(1);
        t.AttackStarted(1, 20f);

        Assert.Equal(DodgeResult.Dodged, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void HitOrParryWithoutAnOpenAttack_ChangesNothing()
    {
        var t = new DodgeTracker<int>();
        t.PlayerHitBy(1);
        t.Parried(1);
        t.PlayerHitByUnknown();
        t.AttackStarted(1, 5f);

        Assert.Equal(DodgeResult.Dodged, t.AttackEnded(1, 5f, MinSpeed));
    }

    [Fact]
    public void StartedAt_IsWhenTheAttackStageBegan()
    {
        var t = Attacking(at: 12.5f);

        Assert.True(t.TryStartedAt(1, out var at));
        Assert.Equal(12.5f, at);
        Assert.False(t.TryStartedAt(2, out _));
    }

    [Fact]
    public void Clear_ForgetsEverything()
    {
        var t = Attacking();
        t.Clear();

        Assert.Equal(DodgeResult.NotTracked, t.AttackEnded(1, 5f, MinSpeed));
    }
}

public class HeadSpeedMeterTests
{
    private const float Window = 0.1f;

    [Fact]
    public void NoSpeed_UntilTheWindowIsCovered()
    {
        var m = new HeadSpeedMeter(Window);

        Assert.Null(m.Add(0f, 0f, 0f));
        Assert.Null(m.Add(0.05f, 0.1f, 0f));
        Assert.NotNull(m.Add(0.1f, 0.2f, 0f));
    }

    [Fact]
    public void SteadyMovement_GivesItsSpeed()
    {
        var m = new HeadSpeedMeter(Window);
        float? speed = null;
        for (var i = 0; i <= 50; i++)
        {
            var t = i * 0.01f;
            speed = m.Add(t, 2f * t, 0f);
        }

        Assert.Equal(2f, speed!.Value, 2);
    }

    [Fact]
    public void PositionsMovingInFixedSteps_BetweenFastFrames_GiveTheTrueSpeed_NotSpikes()
    {
        // The rig moves every physics step (0.02 s) while frames come every 0.004 s: per-frame speeds would read 0 or
        // 5x the true speed; measured over the window they read the true 2.5 m/s (within one physics step).
        var m = new HeadSpeedMeter(Window);
        var peak = 0f;
        for (var i = 0; i <= 500; i++)
        {
            var t = i * 0.004f;
            var stepped = (float)System.Math.Floor(t / 0.02f) * 0.02f;
            var speed = m.Add(t, 2.5f * stepped, 0f);
            if (speed.HasValue && speed.Value > peak)
            {
                peak = speed.Value;
            }
        }

        Assert.InRange(peak, 2.4f, 3.2f);
    }

    [Fact]
    public void Diagonal_IsTheHorizontalDistance()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(0f, 0f, 0f);

        Assert.Equal(5f, m.Add(0.1f, 0.3f, 0.4f)!.Value, 3);
    }

    [Fact]
    public void StandingStill_IsZero()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(0f, 1f, 1f);

        Assert.Equal(0f, m.Add(0.2f, 1f, 1f)!.Value);
    }

    [Fact]
    public void ATeleport_StartsOver()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(0f, 0f, 0f);
        m.Add(0.1f, 0.1f, 0f);

        Assert.Null(m.Add(0.11f, 0.1f + HeadSpeedMeter.MaxStep + 0.01f, 0f));
        Assert.Null(m.Add(0.15f, 1.2f, 0f));
        Assert.NotNull(m.Add(0.22f, 1.3f, 0f));
    }

    [Fact]
    public void AStepJustBelowTheTeleportLimit_Counts()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(0f, 0f, 0f);

        Assert.NotNull(m.Add(0.1f, HeadSpeedMeter.MaxStep - 0.01f, 0f));
    }

    [Fact]
    public void TimeGoingBackwards_StartsOver()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(5f, 0f, 0f);

        Assert.Null(m.Add(1f, 0f, 0f));
        Assert.NotNull(m.Add(1.1f, 0.1f, 0f));
    }

    [Fact]
    public void Clear_StartsOver()
    {
        var m = new HeadSpeedMeter(Window);
        m.Add(0f, 0f, 0f);
        m.Clear();

        Assert.Null(m.Add(0.2f, 0f, 0f));
    }
}

public class CatchTrackerTests
{
    private const float MaxAir = 3f;

    private static CatchTracker<int> Knocked(int weapon = 7, float at = 10f)
    {
        var t = new CatchTracker<int>();
        t.KnockedAway(weapon, at);
        return t;
    }

    [Fact]
    public void PlayerGrab_InTheAir_IsACatch()
    {
        Assert.Equal(CatchResult.Caught, Knocked().Grabbed(7, 11f, byPlayer: true, MaxAir));
    }

    [Fact]
    public void PlayerGrab_AtTheAirTimeLimit_StillCounts()
    {
        Assert.Equal(CatchResult.Caught, Knocked().Grabbed(7, 13f, true, MaxAir));
    }

    [Fact]
    public void PlayerGrab_AfterTheAirTimeLimit_IsTooLate()
    {
        Assert.Equal(CatchResult.TooLate, Knocked().Grabbed(7, 13.01f, true, MaxAir));
    }

    [Fact]
    public void PlayerGrab_AfterItLanded_IsNoCatch()
    {
        var t = Knocked();
        t.Landed(7);

        Assert.Equal(CatchResult.NotTracked, t.Grabbed(7, 10.5f, true, MaxAir));
    }

    [Fact]
    public void NpcGrabbingItFirst_EndsTheChance()
    {
        var t = Knocked();

        Assert.Equal(CatchResult.NotPlayer, t.Grabbed(7, 10.5f, false, MaxAir));
        Assert.Equal(CatchResult.NotTracked, t.Grabbed(7, 10.6f, true, MaxAir));
    }

    [Fact]
    public void AWeaponIsCaughtOnce()
    {
        var t = Knocked();
        t.Grabbed(7, 10.5f, true, MaxAir);

        Assert.Equal(CatchResult.NotTracked, t.Grabbed(7, 10.6f, true, MaxAir));
    }

    [Fact]
    public void AWeaponNobodyKnockedAway_IsNotTracked()
    {
        Assert.Equal(CatchResult.NotTracked, new CatchTracker<int>().Grabbed(7, 1f, true, MaxAir));
    }

    [Fact]
    public void Expired_ListsAndForgetsWeaponsPastTheAirTime()
    {
        var t = Knocked(7, 10f);
        t.KnockedAway(8, 12f);

        Assert.Equal(new[] { 7 }, t.Expired(13.5f, MaxAir));
        Assert.Equal(CatchResult.NotTracked, t.Grabbed(7, 13.5f, true, MaxAir));
        Assert.Equal(CatchResult.Caught, t.Grabbed(8, 13.5f, true, MaxAir));
    }

    [Fact]
    public void Tracked_SaysWhetherAWeaponIsStillInTheAir()
    {
        var t = Knocked();

        Assert.True(t.IsTracked(7));
        t.Landed(7);
        Assert.False(t.IsTracked(7));
    }

    [Fact]
    public void Clear_ListsEveryTrackedWeapon_AndForgetsThem()
    {
        var t = Knocked(7);
        t.KnockedAway(8, 10f);

        Assert.Equal(new[] { 7, 8 }, t.Clear());
        Assert.False(t.IsTracked(7));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Ground_IsWhatBelongsToNoColliderGroup(bool otherHasColliderGroup, bool landed)
    {
        Assert.Equal(landed, CatchTracker<int>.IsGround(otherHasColliderGroup));
    }
}

public class DodgeCatchSettingsTests
{
    [Fact]
    public void Defaults_AsDesigned()
    {
        Assert.Equal(10, AdrenalineSettings.DefaultGainDodge);
        Assert.Equal(10, AdrenalineSettings.DefaultGainCatch);
        Assert.Equal(2f, AdrenalineSettings.DodgeMinSpeed.Default);
        Assert.Equal(3f, AdrenalineSettings.CatchMaxAirTime.Default);
    }

    [Fact]
    public void Defaults_AreOnTheirSliders()
    {
        foreach (var s in new[] { AdrenalineSettings.DodgeMinSpeed, AdrenalineSettings.CatchMaxAirTime })
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name);
        }
    }
}
