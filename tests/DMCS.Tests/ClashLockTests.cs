using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ClashBlockVerdictTests
{
    private const float Point = 0.25f;
    private const float Angle = 45f;

    [Fact]
    public void AtTheFlash_AcrossTheSwing_LocksTheBlades() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0.1f, Point, 20f, Angle));

    [Fact]
    public void AtTheLimits_StillLocks() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(Point, Point, Angle, Angle));

    [Fact]
    public void AwayFromTheFlash_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.OffPoint, ClashRules.JudgeBlock(0.3f, Point, 10f, Angle));

    [Fact]
    public void AtTheFlash_ButAlongTheSwing_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.WrongAngle, ClashRules.JudgeBlock(0.1f, Point, 60f, Angle));

    [Fact]
    public void SwingPlaneUnknown_TheContactAtTheFlashDecides() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(0.1f, Point, null, Angle));

    [Fact]
    public void DistanceIsCheckedFirst() =>
        Assert.Equal(BlockVerdict.OffPoint, ClashRules.JudgeBlock(1f, Point, 80f, Angle));
}

public class ClashLockTests
{
    private const float Max = 4f;

    [Fact]
    public void ALock_RunsUntilItsMaxDuration()
    {
        var l = new ClashLock();
        l.Start(10f);

        Assert.True(l.IsLocked);
        Assert.Equal(LockEnd.None, l.Tick(13.9f, Max));
        Assert.Equal(LockEnd.TimeUp, l.Tick(14f, Max));
        Assert.False(l.IsLocked);
    }

    [Fact]
    public void ALock_EndsOnce()
    {
        var l = new ClashLock();
        l.Start(10f);
        l.Tick(14f, Max);

        Assert.Equal(LockEnd.None, l.Tick(15f, Max));
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
    public void NoLock_NothingToTickOrBreak()
    {
        var l = new ClashLock();

        Assert.Equal(LockEnd.None, l.Tick(1f, Max));
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
        Assert.Equal(4f, ClashSettings.MaxDuration.Default);
    }
}
