using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ClashBlockVerdictTests
{
    private const float Point = 0.25f;
    private const float Angle = 45f;
    private const float MaxSpeed = 1.5f;
    private const float Slow = 0.5f;

    [Fact]
    public void AtTheFlash_AcrossTheSwing_LocksTheBlades() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0.1f, Point, 20f, Angle, Slow, MaxSpeed));

    [Fact]
    public void AtTheLimits_StillLocks() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(Point, Point, Angle, Angle, Slow, MaxSpeed));

    [Fact]
    public void AwayFromTheFlash_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.OffPoint, ClashRules.JudgeBlock(0.3f, Point, 10f, Angle, Slow, MaxSpeed));

    [Fact]
    public void AtTheFlash_ButAlongTheSwing_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.WrongAngle, ClashRules.JudgeBlock(0.1f, Point, 60f, Angle, Slow, MaxSpeed));

    [Fact]
    public void SwingPlaneUnknown_TheContactAtTheFlashDecides() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0.1f, Point, null, Angle, Slow, MaxSpeed));

    [Theory]
    [InlineData(0f)]
    [InlineData(45f)]
    [InlineData(90f)]
    public void Tolerance90_TakesAnyAngle(float angle) =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0f, Point, angle, 90f, Slow, MaxSpeed));

    [Fact]
    public void DistanceIsCheckedFirst() =>
        Assert.Equal(BlockVerdict.OffPoint, ClashRules.JudgeBlock(1f, Point, 80f, Angle, Slow, MaxSpeed));

    // User 2026-11-21: too much swing at the star is no clash (the blade bounces off, nothing happens); less swing locks.
    [Theory]
    [InlineData(2f)]
    [InlineData(MaxSpeed)]
    public void TooMuchSwing_AtTheFlash_AcrossTheSwing_IsNoClash(float handSpeed) =>
        Assert.Equal(BlockVerdict.TooFast, ClashRules.JudgeBlock(0.1f, Point, 20f, Angle, handSpeed, MaxSpeed));

    [Theory]
    [InlineData(0f)]
    [InlineData(1.49f)]
    public void LessSwing_AtTheFlash_AcrossTheSwing_Locks(float handSpeed) =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0.1f, Point, 20f, Angle, handSpeed, MaxSpeed));

    [Fact]
    public void TooMuchSwing_SwingPlaneUnknown_IsNoClash() =>
        Assert.Equal(BlockVerdict.TooFast, ClashRules.JudgeBlock(0.1f, Point, null, Angle, 2f, MaxSpeed));

    [Fact]
    public void TooMuchSwing_AwayFromTheFlash_StaysOffPoint() =>
        Assert.Equal(BlockVerdict.OffPoint, ClashRules.JudgeBlock(0.3f, Point, 20f, Angle, 2f, MaxSpeed));

    [Fact]
    public void TooMuchSwing_AlongTheSwing_StaysWrongAngle() =>
        Assert.Equal(BlockVerdict.WrongAngle, ClashRules.JudgeBlock(0.1f, Point, 60f, Angle, 2f, MaxSpeed));
}

public class ClashLockTests
{
    [Fact]
    public void ALock_RunsUntilTheRoundsDecideIt_AndEndsOnce()
    {
        var l = new ClashLock();
        l.Start(10f);

        Assert.True(l.IsLocked);
        Assert.Equal(LockEnd.Decided, l.Decide());
        Assert.False(l.IsLocked);
        Assert.Equal(LockEnd.None, l.Decide());
        Assert.Equal(LockEnd.None, l.Break());
    }

    [Fact]
    public void Breaking_ALock_EndsItWithTheReason()
    {
        var l = new ClashLock();
        l.Start(10f);

        Assert.Equal(LockEnd.Broken, l.Break());
        Assert.False(l.IsLocked);
        Assert.Equal(LockEnd.None, l.Break());
    }

    [Fact]
    public void NoLock_NothingToDecideOrBreak()
    {
        var l = new ClashLock();

        Assert.Equal(LockEnd.None, l.Decide());
        Assert.Equal(LockEnd.None, l.Break());
    }

    [Fact]
    public void Elapsed_IsTimeSinceTheStart()
    {
        var l = new ClashLock();
        l.Start(10f);

        Assert.Equal(1.5f, l.Elapsed(11.5f), 3);
    }

    [Fact]
    public void Diagnostics_AreDueEveryTenthOfASecond()
    {
        var l = new ClashLock();
        l.Start(10f);

        Assert.True(l.DiagnosticDue(10f));
        Assert.False(l.DiagnosticDue(10.05f));
        Assert.True(l.DiagnosticDue(10.1f));
    }
}

public class ClashLockSettingsTests
{
    [Fact]
    public void Defaults_AsDesigned()
    {
        Assert.Equal(0.25f, ClashSettings.PointTolerance.Default);
        Assert.Equal(20f, ClashSettings.WeaponWeight.Default);
    }

    [Fact]
    public void MaxBlockSpeed_IsOnePointFive_AndStaysUnderThePowerParry()
    {
        Assert.Equal(1.5f, ClashSettings.MaxBlockSpeed.Default);
        Assert.True(ClashSettings.MaxBlockSpeed.Default < ParrySettings.PowerSpeed.Default);
        Assert.Contains(ClashSettings.MaxBlockSpeed, ClashSettings.Sliders);
    }

    [Fact]
    public void BladeHold_IsOnByDefault_WithTwoCentimetresSlack()
    {
        Assert.True(ClashSettings.DefaultHoldBlade);
        Assert.Equal(0.02f, ClashSettings.HoldSlack.Default);
        Assert.Equal(0f, ClashSettings.HoldSlack.Min);
        Assert.Contains(ClashSettings.HoldSlack, ClashSettings.Sliders);
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(0.01f, false)]
    [InlineData(0.02f, false)]
    public void BladeHold_IsRigid_OnlyWithoutSlack(float slack, bool rigid)
    {
        Assert.Equal(rigid, ClashSettings.HoldIsRigid(slack));
    }
}

public class SwingTopTests
{
    private const float WindUp = 0.5f;

    [Fact]
    public void Rising_IsNotTheTopYet()
    {
        var t = new SwingTop(WindUp);

        Assert.False(t.Sample(0.1f, 1.2f));
        Assert.False(t.Sample(0.3f, 1.6f));
    }

    [Fact]
    public void Falling_FromTheHighestPoint_IsTheTop()
    {
        var t = new SwingTop(WindUp);
        t.Sample(0.2f, 1.5f);
        t.Sample(0.35f, 1.8f);

        Assert.False(t.Sample(0.38f, 1.79f), "within the 2 cm jitter");
        Assert.True(t.Sample(0.4f, 1.77f));
    }

    [Fact]
    public void ADropTooEarly_InTheWindUp_DoesNotCount()
    {
        // LungeSwingTopToLeft starts high and dips first (harness probe 2026-11-17): not before 40 % of the wind-up.
        var t = new SwingTop(WindUp);
        t.Sample(0.0f, 1.7f);

        Assert.False(t.Sample(0.1f, 1.5f));
        Assert.True(t.Sample(0.21f, 1.5f));
    }

    [Fact]
    public void NoTopSeen_TheStrikeStartIsTheLatest()
    {
        var t = new SwingTop(WindUp);
        t.Sample(0.2f, 1.2f);

        Assert.True(t.Sample(0.5f, 1.9f));
    }

    [Fact]
    public void TheTopComesOnce()
    {
        var t = new SwingTop(WindUp);
        t.Sample(0.3f, 1.8f);
        Assert.True(t.Sample(0.4f, 1.7f));

        Assert.False(t.Sample(0.45f, 1.6f));
        Assert.True(t.Reached);
    }
}
