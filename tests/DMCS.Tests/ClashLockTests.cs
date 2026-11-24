using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ClashBlockVerdictTests
{
    // User 2026-11-24: a clash needs both hands on the weapon and the two blades meeting, blade on blade
    // (the first rule of the same day, both hands only, also locked on a hit on the hilt or handle).
    [Fact]
    public void BothHands_BladeOnBlade_LocksTheBlades() =>
        Assert.Equal(BlockVerdict.Clash, ClashRules.JudgeBlock(twoHanded: true, bladesMeet: true));

    [Fact]
    public void BothHands_ButNotBladeOnBlade_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.NotBladeOnBlade, ClashRules.JudgeBlock(twoHanded: true, bladesMeet: false));

    [Fact]
    public void OneHand_BladeOnBlade_IsAnOrdinaryParry() =>
        Assert.Equal(BlockVerdict.OneHanded, ClashRules.JudgeBlock(twoHanded: false, bladesMeet: true));

    [Fact]
    public void OneHand_NotBladeOnBlade_NamesTheHandsFirst() =>
        Assert.Equal(BlockVerdict.OneHanded, ClashRules.JudgeBlock(twoHanded: false, bladesMeet: false));
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
        Assert.Equal(20f, ClashSettings.WeaponWeight.Default);
    }

    [Fact]
    public void BladeHold_IsOnByDefault_WithTwoCentimetresSlack()
    {
        Assert.True(ClashSettings.DefaultHoldBlade);
        Assert.Equal(0.02f, ClashSettings.HoldSlack.Default);
        Assert.Equal(0f, ClashSettings.HoldSlack.Min);
        Assert.Contains(ClashSettings.HoldSlack, ClashSettings.Sliders);
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
