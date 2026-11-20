using System;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class QuatTests
{
    private static readonly Vec3 X = new Vec3(1f, 0f, 0f);
    private static readonly Vec3 Y = new Vec3(0f, 1f, 0f);
    private static readonly Vec3 Z = new Vec3(0f, 0f, 1f);

    [Fact]
    public void AngularVelocity_OfATurnAroundOneAxis_IsThatAxisTimesTheRate()
    {
        var from = Quat.AngleAxis(10f, Y);
        var to = Quat.AngleAxis(10f, X) * from; // 10° more around X, in the parent frame

        var w = Quat.AngularVelocity(from, to, 0.02f);

        Assert.Equal(500f, w.X, 1);
        Assert.Equal(0f, w.Y, 1);
        Assert.Equal(0f, w.Z, 1);
    }

    [Fact]
    public void AngularVelocity_TakesTheShorterWayRound()
    {
        var from = Quat.Identity;
        var to = Quat.AngleAxis(350f, Z);

        var w = Quat.AngularVelocity(from, to, 1f);

        Assert.Equal(-10f, w.Z, 1);
    }

    [Fact]
    public void AngularVelocity_NoTurnOrNoTime_IsZero()
    {
        Assert.Equal(0f, Quat.AngularVelocity(Quat.Identity, Quat.Identity, 0.1f).Length, 4);
        Assert.Equal(0f, Quat.AngularVelocity(Quat.Identity, Quat.AngleAxis(30f, Z), 0f).Length, 4);
    }
}

public class RollMeterTests
{
    private static readonly Vec3 Blade = new Vec3(0f, 0f, 1f);

    [Fact]
    public void Roll_IsTheTurnAroundTheBlade_NotTheSwing()
    {
        // A swing turns the blade around a vertical axis: no roll.
        Assert.Equal(0f, RollMeter.RollRate(new Vec3(0f, 800f, 0f), Blade), 2);
        // A wrist twist turns it around its own long axis: all roll, either way.
        Assert.Equal(600f, RollMeter.RollRate(new Vec3(0f, 0f, -600f), Blade), 2);
        // A swing with some twist: only the twist counts.
        Assert.Equal(300f, RollMeter.RollRate(new Vec3(0f, 800f, 300f), Blade), 2);
        Assert.Equal(0f, RollMeter.RollRate(new Vec3(0f, 0f, 600f), Vec3.Zero), 2);
    }

    [Fact]
    public void Meter_MeasuresOverItsWindow()
    {
        var m = new RollMeter(0.03f);
        var q = Quat.Identity;
        Vec3? last = null;
        for (var i = 0; i <= 10; i++)
        {
            // 600 °/s around the blade: 6° every 0.01 s
            last = m.Add(i * 0.01f, q) ?? last;
            q = Quat.AngleAxis(6f, Blade) * q;
        }

        Assert.NotNull(last);
        Assert.Equal(600f, RollMeter.RollRate(last!.Value, Blade), 0);
    }

    [Fact]
    public void Meter_NeedsAFullWindowFirst_AndForgetsOnClear()
    {
        var m = new RollMeter(0.03f);

        Assert.Null(m.Add(0f, Quat.Identity));
        Assert.Null(m.Add(0.01f, Quat.Identity));
        Assert.NotNull(m.Add(0.03f, Quat.Identity));
        m.Clear();
        Assert.Null(m.Add(1f, Quat.Identity));
    }
}

public class TwistRuleTests
{
    private static TwistContact C(
        bool enabled = true, bool playerWeapon = true, bool enemyWeapon = true, bool dead = false, bool shield = false,
        bool inLock = false, bool cooling = false, float rate = 500f, float min = 400f, bool require = true, bool staggered = true,
        float roll = 0.1f, float chance = 0.5f) =>
        new TwistContact(enabled, playerWeapon, enemyWeapon, dead, shield, inLock, cooling, rate, min, require, staggered, roll, chance);

    [Fact]
    public void FastTwist_OnAnEnemyWeapon_Disarms() => Assert.Equal(TwistDecision.Disarm, TwistRule.Decide(C()));

    [Theory]
    [InlineData(TwistDecision.Disabled)]
    [InlineData(TwistDecision.NotPlayerWeapon)]
    [InlineData(TwistDecision.NotEnemyWeapon)]
    [InlineData(TwistDecision.EnemyDeadOrPlayer)]
    [InlineData(TwistDecision.KeepsShield)]
    [InlineData(TwistDecision.InLock)]
    [InlineData(TwistDecision.Cooldown)]
    [InlineData(TwistDecision.TooSlow)]
    [InlineData(TwistDecision.NotStaggered)]
    [InlineData(TwistDecision.ChanceFailed)]
    public void EachRefusal_HasItsReason(TwistDecision expected)
    {
        var c = expected switch
        {
            TwistDecision.Disabled => C(enabled: false),
            TwistDecision.NotPlayerWeapon => C(playerWeapon: false),
            TwistDecision.NotEnemyWeapon => C(enemyWeapon: false),
            TwistDecision.EnemyDeadOrPlayer => C(dead: true),
            TwistDecision.KeepsShield => C(shield: true),
            TwistDecision.InLock => C(inLock: true),
            TwistDecision.Cooldown => C(cooling: true),
            TwistDecision.TooSlow => C(rate: 399f),
            TwistDecision.NotStaggered => C(staggered: false),
            _ => C(roll: 0.5f),
        };

        Assert.Equal(expected, TwistRule.Decide(c));
    }

    [Fact]
    public void Stagger_IsNeededOnlyWhileRequired()
    {
        Assert.Equal(TwistDecision.Disarm, TwistRule.Decide(C(require: false, staggered: false)));
        Assert.Equal(TwistDecision.NotStaggered, TwistRule.Decide(C(require: true, staggered: false)));
        Assert.Equal(TwistDecision.Disarm, TwistRule.Decide(C(require: true, staggered: true)));
    }

    [Fact]
    public void ASlowTouch_IsTooSlow_EvenWhileCoolingDown_SoItNeverBlocksATwist()
    {
        Assert.Equal(TwistDecision.TooSlow, TwistRule.Decide(C(rate: 100f, cooling: true)));
        Assert.Equal(TwistDecision.Cooldown, TwistRule.Decide(C(rate: 500f, cooling: true)));
    }

    [Fact]
    public void ASlowTwist_IsTooSlow_EvenUnstaggered()
    {
        // The roll is checked first: an unstaggered enemy is only named when the twist itself was fast enough.
        Assert.Equal(TwistDecision.TooSlow, TwistRule.Decide(C(rate: 100f, staggered: false)));
    }

    [Fact]
    public void ExactlyTheMinimum_Counts_AndChanceZeroNeverDisarms()
    {
        Assert.Equal(TwistDecision.Disarm, TwistRule.Decide(C(rate: 400f)));
        Assert.Equal(TwistDecision.ChanceFailed, TwistRule.Decide(C(roll: 0f, chance: 0f)));
        Assert.Equal(TwistDecision.Disarm, TwistRule.Decide(C(roll: 0.999f, chance: 1f)));
    }

    [Fact]
    public void OnlyRealBladeContacts_AreLogged()
    {
        Assert.True(TwistRule.IsQuiet(TwistDecision.Disabled));
        Assert.True(TwistRule.IsQuiet(TwistDecision.NotPlayerWeapon));
        Assert.True(TwistRule.IsQuiet(TwistDecision.NotEnemyWeapon));
        Assert.True(TwistRule.IsQuiet(TwistDecision.EnemyDeadOrPlayer));
        Assert.True(TwistRule.IsQuiet(TwistDecision.Cooldown));
        Assert.False(TwistRule.IsQuiet(TwistDecision.TooSlow));
        Assert.False(TwistRule.IsQuiet(TwistDecision.NotStaggered));
        Assert.False(TwistRule.IsQuiet(TwistDecision.ChanceFailed));
        Assert.False(TwistRule.IsQuiet(TwistDecision.KeepsShield));
        Assert.False(TwistRule.IsQuiet(TwistDecision.InLock));
        Assert.False(TwistRule.IsQuiet(TwistDecision.Disarm));
    }

    [Fact]
    public void Cooldown_PerEnemy_EndsAfterItsTime()
    {
        var c = new ContactCooldown<int>();
        c.Mark(1, 10f, 0.5f);

        Assert.True(c.IsCoolingDown(1, 10.4f));
        Assert.False(c.IsCoolingDown(1, 10.5f));
        Assert.False(c.IsCoolingDown(2, 10.1f));
        c.Clear();
        Assert.False(c.IsCoolingDown(1, 10.1f));
    }

    [Fact]
    public void Settings_AsSpecified()
    {
        Assert.True(TwistSettings.DefaultEnabled);
        Assert.Equal(100f, TwistSettings.Chance.Default);
        Assert.Equal(400f, TwistSettings.MinRollSpeed.Default);
        foreach (var s in TwistSettings.Sliders)
        {
            Assert.Contains(s.Values(), v => Math.Abs(v - s.Default) < 0.001f);
        }
    }
}
