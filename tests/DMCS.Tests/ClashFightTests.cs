using System;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ClashModeTests
{
    [Fact]
    public void Default_IsSimple_AndTheListIsOffSimpleOn()
    {
        Assert.Equal(ClashModes.Simple, ClashModes.Default);
        Assert.Equal(new[] { "Off", "Simple", "On" }, ClashModes.All);
    }

    [Theory]
    [InlineData("Off", false, false)]
    [InlineData("Simple", true, false)]
    [InlineData("On", true, true)]
    [InlineData("whatever", true, false)]
    [InlineData(null, true, false)]
    public void Mode_DecidesActiveAndLock(string mode, bool active, bool locks)
    {
        Assert.Equal(active, ClashModes.IsActive(mode));
        Assert.Equal(locks, ClashModes.Locks(mode));
    }

    [Theory]
    [InlineData("Disarm + Hit", true, true)]
    [InlineData("Disarm", true, false)]
    [InlineData("Hit", false, true)]
    [InlineData("None", false, false)]
    public void LoseOutcome_SaysWhatHappens(string outcome, bool disarms, bool hits)
    {
        Assert.Equal(disarms, ClashLoseOutcomes.Disarms(outcome));
        Assert.Equal(hits, ClashLoseOutcomes.Hits(outcome));
    }

    [Fact]
    public void Settings_DefaultsAsDecided()
    {
        Assert.Equal(50f, ClashFightSettings.Difficulty.Default);
        Assert.Equal(0f, ClashFightSettings.Difficulty.Min);
        Assert.Equal(200f, ClashFightSettings.Difficulty.Max);
        Assert.Equal(50f, ClashFightSettings.FemaleStrength.Default);
        Assert.Equal(3f, ClashFightSettings.MinRounds.Default);
        Assert.Equal(5f, ClashFightSettings.MaxRounds.Default);
        Assert.Equal(1f, ClashFightSettings.MinRounds.Min);
        Assert.Equal(10f, ClashFightSettings.MaxRounds.Max);
        Assert.Equal(25f, ClashFightSettings.WinKneelChance.Default);
        Assert.Equal(20f, ClashFightSettings.LoseDamage.Default);
        Assert.Equal(ClashLoseOutcomes.DisarmAndHit, ClashFightSettings.DefaultLoseOutcome);
        Assert.True(ClashFightSettings.DefaultWinDisarms);
        Assert.True(ClashFightSettings.DefaultScalesWithHeight);
        Assert.True(ClashFightSettings.DefaultScalesWithGender);
    }

    [Fact]
    public void EverySlider_DefaultLiesOnItsSteps()
    {
        foreach (var s in ClashFightSettings.Sliders)
        {
            Assert.Contains(s.Values(), v => Math.Abs(v - s.Default) < 0.0001f);
        }
    }
}

public class ClashStrengthTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(50f, 1f)]
    [InlineData(100f, 2f)]
    [InlineData(200f, 4f)]
    public void Difficulty_IsLinear_50IsOne(float difficulty, float factor)
    {
        Assert.Equal(factor, ClashStrength.Factor(difficulty, false, 2f, 1.8f, false, true, 50f), 4);
    }

    [Fact]
    public void Height_TallerStronger_SmallerWeaker_OnlyWhenOn()
    {
        Assert.Equal(1.1f, ClashStrength.Factor(50f, true, 1.98f, 1.8f, false, false, 50f), 3);
        Assert.Equal(0.9f, ClashStrength.Factor(50f, true, 1.62f, 1.8f, false, false, 50f), 3);
        Assert.Equal(1f, ClashStrength.Factor(50f, false, 1.98f, 1.8f, false, false, 50f), 3);
        Assert.Equal(1f, ClashStrength.Factor(50f, true, 0f, 1.8f, false, false, 50f), 3);
    }

    [Fact]
    public void Gender_FemaleWeakerByTheReduction_OnlyWhenOn()
    {
        Assert.Equal(0.5f, ClashStrength.Factor(50f, false, 1.7f, 1.8f, true, true, 50f), 3);
        Assert.Equal(1f, ClashStrength.Factor(50f, false, 1.7f, 1.8f, true, false, 50f), 3);
        Assert.Equal(1f, ClashStrength.Factor(50f, false, 1.7f, 1.8f, false, true, 50f), 3);
        Assert.Equal(0.45f, ClashStrength.Factor(100f, true, 1.62f, 1.8f, true, true, 75f), 3);
    }

    [Fact]
    public void Weaker_IsSmallerOrFemale_PerSwitch()
    {
        Assert.True(ClashStrength.IsWeaker(true, 1.6f, 1.8f, false, false));
        Assert.False(ClashStrength.IsWeaker(false, 1.6f, 1.8f, false, false));
        Assert.False(ClashStrength.IsWeaker(true, 1.9f, 1.8f, false, false));
        Assert.True(ClashStrength.IsWeaker(false, 1.9f, 1.8f, true, true));
        Assert.False(ClashStrength.IsWeaker(false, 1.9f, 1.8f, false, true));
        Assert.True(ClashStrength.IsWeaker(true, 1.6f, 1.8f, true, true));
    }

    [Theory]
    [InlineData(0.0, 3)]
    [InlineData(0.34, 4)]
    [InlineData(0.99, 5)]
    [InlineData(1.0, 5)]
    public void Rounds_RandomBetweenMinAndMax(double roll, int rounds)
    {
        Assert.Equal(rounds, ClashStrength.Rounds(3, 5, roll, false, 1));
    }

    [Fact]
    public void Rounds_FewerAgainstWeaker_NeverBelowOne_MinMaxSwappedIsFine()
    {
        Assert.Equal(2, ClashStrength.Rounds(3, 5, 0.0, true, 1));
        Assert.Equal(1, ClashStrength.Rounds(1, 1, 0.5, true, 3));
        Assert.Equal(4, ClashStrength.Rounds(4, 4, 0.7, false, 1));
        Assert.Equal(3, ClashStrength.Rounds(5, 3, 0.0, false, 0));
    }
}

public class ClashRoundsTests
{
    // 2 s push phase, 1 s return window, enemy peak 0.06 m, dead zone 0.03 m, back within 0.08 m.
    private static ClashRounds New(int rounds = 3) => new ClashRounds(rounds, 2f, 1f, 0.06f, 0.03f, 0.08f);

    /// <summary>Pushes a whole phase with a constant lead in 0.02 s steps; returns the last step.</summary>
    private static ClashStep PushPhase(ClashRounds r, float lead)
    {
        var last = ClashStep.None;
        for (var i = 0; i < 200 && last == ClashStep.None; i++)
        {
            last = r.Tick(0.02f, lead, lead);
        }
        return last;
    }

    [Fact]
    public void EnemyPush_SwellsAndEbbsOnce()
    {
        var r = New();

        Assert.Equal(0f, r.EnemyPushAt(0f), 4);
        Assert.Equal(0.06f, r.EnemyPushAt(1f), 4);
        Assert.Equal(0.06f * (float)Math.Sin(Math.PI / 4), r.EnemyPushAt(0.5f), 4);
        Assert.Equal(0f, r.EnemyPushAt(2f), 4);
    }

    [Fact]
    public void PlayerPush_IsTheLeadBeyondTheDeadZone()
    {
        var r = New();

        Assert.Equal(0f, r.PlayerPush(0.02f), 4);
        Assert.Equal(0.05f, r.PlayerPush(0.08f), 4);
        Assert.Equal(0f, r.PlayerPush(-0.1f), 4);
    }

    [Fact]
    public void EnemySum_IsTheHalfSineIntegral()
    {
        var r = New();
        PushPhase(r, 0f);

        // ∫0..2 0.06·sin(πt/2) dt = 0.06 · 4/π ≈ 0.0764
        Assert.Equal(0.06f * 4f / (float)Math.PI, r.EnemySum, 4);
    }

    [Fact]
    public void NotPushing_LosesTheFirstPushPhase()
    {
        var r = New();

        Assert.Equal(ClashStep.LostPush, PushPhase(r, 0.02f));
        Assert.Equal(ClashPhase.Over, r.Phase);
    }

    [Fact]
    public void Holding_ThenReturning_GoesThroughEveryRound_ToAWin()
    {
        var r = New(3);
        var lead = 0.03f + 0.05f; // 0.05 m beyond the dead zone, more than the enemy's mean 0.038 m

        Assert.Equal(ClashStep.PushHeld, PushPhase(r, lead));
        Assert.Equal(ClashPhase.Return, r.Phase);
        Assert.Equal(ClashStep.None, r.Tick(0.1f, 0.2f, 0.2f));
        Assert.Equal(ClashStep.Returned, r.Tick(0.1f, 0f, 0.05f));
        Assert.Equal(2, r.Round);
        Assert.Equal(0f, r.PlayerSum);
        Assert.Equal(ClashStep.PushHeld, PushPhase(r, lead));
        Assert.Equal(ClashStep.Returned, r.Tick(0.1f, 0f, 0.0f));
        Assert.Equal(ClashStep.Won, PushPhase(r, lead));
        Assert.Equal(ClashPhase.Over, r.Phase);
        Assert.Equal(ClashStep.None, r.Tick(0.1f, lead, lead));
    }

    [Fact]
    public void NotComingBack_LosesWhenTheReturnWindowRunsOut()
    {
        var r = New(2);
        PushPhase(r, 0.1f);

        Assert.Equal(ClashStep.None, r.Tick(0.5f, 0.1f, 0.1f));
        Assert.Equal(ClashStep.LostReturn, r.Tick(0.5f, 0.1f, 0.1f));
    }

    [Fact]
    public void OneRound_WinsWithTheFirstHeldPhase()
    {
        Assert.Equal(ClashStep.Won, PushPhase(New(1), 0.1f));
    }

    [Fact]
    public void ALateRound_CanStillBeLost()
    {
        var r = New(3);
        PushPhase(r, 0.1f);
        r.Tick(0.05f, 0f, 0f);

        Assert.Equal(ClashStep.LostPush, PushPhase(r, 0.04f));
    }

    [Fact]
    public void NoEnemyPush_AnyLeadHolds_ZeroTimeStepsDoNothing()
    {
        var r = new ClashRounds(1, 1f, 1f, 0f, 0.03f, 0.08f);

        Assert.Equal(ClashStep.None, r.Tick(0f, 0.5f, 0.5f));
        Assert.Equal(ClashStep.Won, PushPhase(r, 0f));
    }

    [Fact]
    public void ALongFrame_DoesNotOvershootThePhase()
    {
        var r = New(2);

        Assert.Equal(ClashStep.PushHeld, r.Tick(5f, 0.2f, 0.2f));
        Assert.Equal(0.17f * 2f, r.PlayerSum, 4);
    }
}
