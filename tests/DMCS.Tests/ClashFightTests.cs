using System;
using System.Linq;
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
        // 2026-11-22: a forward win no longer disarms by default; a sideways win always does (Clash Side Push).
        Assert.False(ClashFightSettings.DefaultWinDisarms);
        Assert.True(ClashFightSettings.DefaultSidePush);
        Assert.True(ClashFightSettings.DefaultScalesWithHeight);
        Assert.True(ClashFightSettings.DefaultScalesWithGender);
        Assert.True(ClashFightSettings.DefaultDuelStar);
        Assert.True(ClashFightSettings.DefaultOutcomeCues);
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

// The duel as a pure push contest (user 2026-11-22: the return phase is gone; each round is pushed on its own and the
// next one starts at once).
public class ClashRoundsTests
{
    // 2 s push rounds, enemy peak 0.06 m, dead zone 0.03 m.
    private static ClashRounds New(int rounds = 3) => new ClashRounds(rounds, 2f, 0.06f, 0.03f);

    /// <summary>Pushes a whole round with a constant lead in 0.02 s steps; returns the last step.</summary>
    private static ClashStep PushRound(ClashRounds r, float lead)
    {
        var last = ClashStep.None;
        for (var i = 0; i < 200 && last == ClashStep.None; i++)
        {
            last = r.Tick(0.02f, lead, 0f);
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
        var r = New(1);
        PushRound(r, 0f);

        // ∫0..2 0.06·sin(πt/2) dt = 0.06 · 4/π ≈ 0.0764
        Assert.Equal(0.06f * 4f / (float)Math.PI, r.EnemySum, 4);
    }

    [Fact]
    public void NotPushing_LosesTheFirstRound()
    {
        var r = New();

        Assert.Equal(ClashStep.LostPush, PushRound(r, 0.02f));
        Assert.Equal(ClashPhase.Over, r.Phase);
    }

    [Fact]
    public void Holding_GoesStraightIntoTheNextRound_ToAWin()
    {
        var r = New(3);
        var lead = 0.03f + 0.05f; // 0.05 m beyond the dead zone, more than the enemy's mean 0.038 m

        Assert.Equal(ClashStep.PushHeld, PushRound(r, lead));
        Assert.Equal(ClashPhase.Push, r.Phase);
        Assert.Equal(2, r.Round);
        Assert.Equal(0f, r.PlayerSum);
        Assert.Equal(0f, r.EnemySum);
        Assert.Equal(ClashStep.PushHeld, PushRound(r, lead));
        Assert.Equal(3, r.Round);
        Assert.Equal(ClashStep.Won, PushRound(r, lead));
        Assert.Equal(ClashPhase.Over, r.Phase);
        Assert.Equal(ClashStep.None, r.Tick(0.1f, lead, 0f));
    }

    [Fact]
    public void OneRound_WinsWithTheFirstHeldRound()
    {
        Assert.Equal(ClashStep.Won, PushRound(New(1), 0.1f));
    }

    [Fact]
    public void ALateRound_CanStillBeLost()
    {
        var r = New(3);
        PushRound(r, 0.1f);

        Assert.Equal(ClashStep.LostPush, PushRound(r, 0.04f));
    }

    [Fact]
    public void NoEnemyPush_AnyLeadHolds_ZeroTimeStepsDoNothing()
    {
        var r = new ClashRounds(1, 1f, 0f, 0.03f);

        Assert.Equal(ClashStep.None, r.Tick(0f, 0.5f, 0f));
        Assert.Equal(ClashStep.Won, PushRound(r, 0f));
    }

    [Fact]
    public void ALongFrame_DoesNotOvershootTheRound()
    {
        var r = New(2);

        Assert.Equal(ClashStep.PushHeld, r.Tick(5f, 0.2f, 0f));
        Assert.Equal(0f, r.PlayerSum);
        Assert.Equal(2, r.Round);
    }

    [Fact]
    public void Advantage_IsEvenAtTheStartOfEachRound()
    {
        var r = New(2);

        Assert.Equal(0f, r.Advantage);
        PushRound(r, 0.2f);
        Assert.Equal(0f, r.Advantage);
    }

    [Fact]
    public void Advantage_PositiveWhilePushingHarder_NegativeWhileNot_WithinOne()
    {
        var ahead = New(1);
        var behind = New(1);
        for (var i = 0; i < 50; i++)
        {
            ahead.Tick(0.02f, 0.2f, 0f);
            behind.Tick(0.02f, 0f, 0f);
        }

        Assert.InRange(ahead.Advantage, 0.01f, 1f);
        Assert.InRange(behind.Advantage, -1f, -0.01f);
    }

    [Fact]
    public void Advantage_IsClampedToOne()
    {
        var r = New(1);
        for (var i = 0; i < 90; i++)
        {
            r.Tick(0.02f, 5f, 0f);
        }

        Assert.Equal(1f, r.Advantage);
    }

    [Fact]
    public void Advantage_WithoutEnemyPush_AnyPushIsFullyAhead()
    {
        var r = new ClashRounds(1, 2f, 0f, 0.03f);
        Assert.Equal(0f, r.Advantage);

        r.Tick(0.1f, 0.1f, 0f);

        Assert.Equal(1f, r.Advantage);
    }

    // User 2026-11-22: the push can go sideways too; a duel won mostly sideways always disarms.
    [Fact]
    public void SidewaysPush_HoldsARoundLikeAForwardOne()
    {
        var forward = New(1);
        var side = New(1);
        var last = ClashStep.None;
        for (var i = 0; i < 200 && last == ClashStep.None; i++)
        {
            forward.Tick(0.02f, 0.08f, 0f);
            last = side.Tick(0.02f, 0f, -0.08f);
        }

        Assert.Equal(ClashStep.Won, last);
        Assert.Equal(forward.LastPlayerSum, side.LastPlayerSum, 4);
    }

    [Fact]
    public void Sideways_IsWhereMostOfThePushWent_OverTheWholeDuel()
    {
        var r = New(2);
        for (var i = 0; i < 100; i++)
        {
            r.Tick(0.02f, 0.1f, 0f);
        }
        for (var i = 0; i < 100; i++)
        {
            r.Tick(0.02f, 0f, 0.12f);
        }

        Assert.Equal(ClashPhase.Over, r.Phase);
        Assert.True(r.Sideways);
        Assert.Equal(1, r.SideSign);
    }

    [Fact]
    public void ForwardMostly_IsNotSideways()
    {
        var r = New(1);
        for (var i = 0; i < 100; i++)
        {
            r.Tick(0.02f, 0.1f, i < 20 ? -0.2f : 0f);
        }

        Assert.False(r.Sideways);
    }

    [Fact]
    public void ToTheLeft_SideSignIsNegative()
    {
        var r = New(1);
        for (var i = 0; i < 100; i++)
        {
            r.Tick(0.02f, 0f, -0.1f);
        }

        Assert.True(r.Sideways);
        Assert.Equal(-1, r.SideSign);
    }

    [Fact]
    public void NoPush_IsNeitherWay()
    {
        var r = New(1);
        r.Tick(0.5f, 0.01f, 0.01f);

        Assert.False(r.Sideways);
        Assert.Equal(0, r.SideSign);
        Assert.False(r.PushingSideways);
    }

    [Fact]
    public void PushingSideways_FollowsTheLastStep()
    {
        var r = New(1);

        r.Tick(0.02f, 0.02f, 0.1f);
        Assert.True(r.PushingSideways);
        r.Tick(0.02f, 0.1f, 0.02f);
        Assert.False(r.PushingSideways);
    }
}

// What the player sees and feels in the duel (user 2026-11-22: "unclear whether you win or lose").
public class ClashDuelSignalTests
{
    [Fact]
    public void Even_IsTheWarmWhiteOfTheFlash_AtItsSize()
    {
        var (r, g, b) = ClashDuelSignal.Tint(0f);

        Assert.Equal(1f, r, 3);
        Assert.Equal(0.92f, g, 3);
        Assert.Equal(0.7f, b, 3);
        Assert.Equal(1f, ClashDuelSignal.Size(0f), 3);
    }

    [Fact]
    public void Ahead_TurnsGreen_AndGrows()
    {
        var (r, g, b) = ClashDuelSignal.Tint(1f);

        Assert.True(g > r && g > b);
        Assert.True(ClashDuelSignal.Size(1f) > ClashDuelSignal.Size(0.5f));
        Assert.True(ClashDuelSignal.Size(0.5f) > 1f);
    }

    [Fact]
    public void Behind_TurnsRed_AndShrinks()
    {
        var (r, g, b) = ClashDuelSignal.Tint(-1f);

        Assert.True(r > g && r > b);
        Assert.True(ClashDuelSignal.Size(-1f) < 1f);
    }

    [Fact]
    public void OutOfRange_IsClamped()
    {
        Assert.Equal(ClashDuelSignal.Tint(1f), ClashDuelSignal.Tint(3f));
        Assert.Equal(ClashDuelSignal.Size(-1f), ClashDuelSignal.Size(-7f));
    }

    [Fact]
    public void AheadPushingSideways_TurnsGold_BehindStaysRed()
    {
        var (r, g, b) = ClashDuelSignal.Tint(1f, sideways: true);

        Assert.True(r > b && g > b, "gold");
        Assert.NotEqual(ClashDuelSignal.Tint(1f), ClashDuelSignal.Tint(1f, sideways: true));
        Assert.Equal(ClashDuelSignal.Tint(-1f), ClashDuelSignal.Tint(-1f, sideways: true));
        Assert.Equal(ClashDuelSignal.Tint(0f), ClashDuelSignal.Tint(0f, sideways: true));
    }

    [Fact]
    public void WinHaptics_AreAFewStrongQuickBeats()
    {
        var ticks = ClashDuelSignal.Haptics(won: true);

        Assert.InRange(ticks.Count, 2, 4);
        Assert.All(ticks, t => Assert.Equal(1f, t.Strength));
        Assert.True(ticks.Last().Time <= 0.3f);
    }

    [Fact]
    public void LoseHaptics_AreALongerWeakerRumble()
    {
        var lose = ClashDuelSignal.Haptics(won: false);
        var win = ClashDuelSignal.Haptics(won: true);

        Assert.True(lose.Count > win.Count);
        Assert.True(lose.Last().Time > win.Last().Time);
        Assert.All(lose, t => Assert.True(t.Strength < 1f));
        Assert.Equal(lose.OrderBy(t => t.Time).Select(t => t.Time), lose.Select(t => t.Time));
    }
}
