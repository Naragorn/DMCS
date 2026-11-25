using System.Linq;
using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class ClashWeaponRuleTests
{
    [Theory]
    [InlineData("Swords", "SwordShortCommon", true)]
    [InlineData("Greatswords", "SwordGreatDalgarian", true)]
    [InlineData("swords", "Whatever", true)]
    [InlineData("Rakta Weapons", "SwordRaktaKatana", true)]
    [InlineData("Daggers", "DaggerCommon", false)]
    [InlineData("Axes", "AxeShortCommon", false)]
    [InlineData(null, null, false)]
    public void IsSword_ByCategoryOrSwordId(string category, string id, bool sword)
    {
        Assert.Equal(sword, ClashRules.IsSword(category, id));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, true, true)]
    [InlineData(false, true, false, false)]
    public void CanFlash_NeedsASlashBlade_AndASwordWhileOnlySwords(bool onlySwords, bool isSword, bool hasSlashBlade, bool can)
    {
        Assert.Equal(can, ClashRules.CanFlash(onlySwords, isSword, hasSlashBlade));
    }

    [Theory]
    [InlineData(false, 0.5f, true)]
    [InlineData(false, 0.4f, true)]
    [InlineData(false, 0.35f, false)]
    [InlineData(true, 0.7f, false)]
    public void IsBigSwing_LongWindUp_NoThrust(bool thrust, float windUp, bool big)
    {
        Assert.Equal(big, ClashRules.IsBigSwing(thrust, windUp, 0.4f));
    }
}

// A thrust never flashes. The game labels each attack Swing or Thrust (AttackMotion.attackMotion), but its own data labels
// one stab a Swing: CounterThrust (one-handed sword stance, wind-up 0.76 s, animation
// Bas.Animation.Attack.Sword1h.Counter.Stab). It flashed in the headset (Player.log 2026-11-22 13:17:42). So the
// animation's own name counts too.
public class ClashThrustTests
{
    private const string CounterThrustAnimation = "Bas.Animation.Attack.Sword1h.Counter.Stab";

    [Theory]
    [InlineData(true, "Bas.Animation.Attack.Sword1h.Stance2.Stab", true)]
    [InlineData(true, "Bas.Animation.Attack.Sword1h.Stance1.RtLSwing", true)]
    [InlineData(true, null, true)]
    [InlineData(false, CounterThrustAnimation, true)]
    [InlineData(false, "bas.animation.attack.sword1h.counter.stab", true)]
    [InlineData(false, "Stab", true)]
    [InlineData(false, "Bas.Animation.Attack.Sword1h.Counter.SwingRight", false)]
    [InlineData(false, "Bas.Animation.Stab.Attack.Swing", false)]
    [InlineData(false, "", false)]
    [InlineData(false, null, false)]
    public void IsThrust_LabelledThrust_OrAStabAnimation(bool labelledThrust, string address, bool thrust)
    {
        Assert.Equal(thrust, ClashRules.IsThrust(labelledThrust, address));
    }

    [Fact]
    public void CounterThrust_NoStar_AtTheDefaultWindUp()
    {
        Assert.False(ClashRules.IsBigSwing(ClashRules.IsThrust(false, CounterThrustAnimation), 0.76f, 0.45f));
    }

    // Every attack of the game 1.3.2 (the three melee stances, Stance_HumanMelee*.json): the eight labelled thrusts and
    // CounterThrust are thrusts, no swing is.
    [Theory]
    [MemberData(nameof(GameAttacks))]
    public void GameAttacks_1_3_2(string attack, bool labelledThrust, string address, bool thrust)
    {
        Assert.True(thrust == ClashRules.IsThrust(labelledThrust, address), attack);
    }

    public static TheoryData<string, bool, string, bool> GameAttacks => new TheoryData<string, bool, string, bool>
    {
        { "1h/SwingRightToLeft", false, "Bas.Animation.Attack.Sword1h.Stance1.RtLSwing", false },
        { "1h/SwingRightToLeftStep", false, "Bas.Animation.Attack.Sword1h.Stance1.SlashRtLStep", false },
        { "1h/SwingLeftToRight", false, "Bas.Animation.Attack.Sword1h.Stance1.LtRSwing", false },
        { "1h/SwingLeftToRightStep", false, "Bas.Animation.Attack.Sword1h.Stance1.LtRSwingStep", false },
        { "1h/SwingCombo", false, "Bas.Animation.Attack.Sword1h.Stance1.Combo", false },
        { "1h/Thrust", true, "Bas.Animation.Attack.Sword1h.Stance2.Stab", true },
        { "1h/LungeThrust", true, "Bas.Animation.Attack.Sword1h.Stance2.StabLunge", true },
        { "1h/SwingTopToLeft", false, "Bas.Animation.Attack.Sword1h.Stance3.TtLSwing", false },
        { "1h/SwingTopToLeftStep", false, "Bas.Animation.Attack.Sword1h.Stance3.TtLSwingStep", false },
        { "1h/LungeSwingTopToLeft", false, "Bas.Animation.Attack.Sword1h.Stance3.TtLSwingLunge", false },
        { "1h/SwingTopToRight", false, "Bas.Animation.Attack.Sword1h.Stance3.TtRSwing", false },
        { "1h/LungeSwingTopToRight", false, "Bas.Animation.Attack.Sword1h.Stance3.TtRSwingLunge", false },
        { "1h/CounterSwingRight", false, "Bas.Animation.Attack.Sword1h.Counter.RightSlash", false },
        { "1h/CounterSwingLeft", false, "Bas.Animation.Attack.Sword1h.Counter.LeftSlash", false },
        { "1h/CounterSwingHighRight", false, "Bas.Animation.Attack.Sword1h.Counter.HighRight", false },
        { "1h/CounterSwingHighLeft", false, "Bas.Animation.Attack.Sword1h.Counter.HighLeft", false },
        { "1h/CounterThrust", false, "Bas.Animation.Attack.Sword1h.Counter.Stab", true },
        { "1h/RiposteHighRight", false, "Bas.Animation.Attack.Sword1h.Riposte.HighRight", false },
        { "1h/RiposteHighLeft", false, "Bas.Animation.Attack.Sword1h.Riposte.HighLeft", false },
        { "1h/RiposteRight", false, "Bas.Animation.Attack.Sword1h.Riposte.Right", false },
        { "1h/RiposteLeft", false, "Bas.Animation.Attack.Sword1h.Riposte.Left", false },
        { "1h/RiposteLowRight", false, "Bas.Animation.Attack.Sword1h.Riposte.LowRight", false },
        { "1h/RiposteLowLeft", false, "Bas.Animation.Attack.Sword1h.Riposte.LowLeft", false },
        { "DualWield/MidLeftSwing", false, "Bas.Animation.Attack.DualWield.MidLft", false },
        { "DualWield/MidRightSwing", false, "Bas.Animation.Attack.DualWield.MidRgt", false },
        { "DualWield/LowLeftSwing", false, "Bas.Animation.Attack.DualWield.LowLft", false },
        { "DualWield/LowRightSwing", false, "Bas.Animation.Attack.DualWield.LowRgt", false },
        { "DualWield/ThrustLeft", true, "Bas.Animation.Attack.DualWield.StabLft", true },
        { "DualWield/ThrustRight", true, "Bas.Animation.Attack.DualWield.StabRgt", true },
        { "DualWield/HighLeftSwing", false, "Bas.Animation.Attack.DualWield.HighLft", false },
        { "DualWield/HighRightSwing", false, "Bas.Animation.Attack.DualWield.HighRgt", false },
        { "DualWield/CrossSlash", false, "Bas.Animation.Attack.DualWield.CrossSlash", false },
        { "DualWield/ChainMidLeftSwing", false, "Bas.Animation.Attack.DualWield.Counter.MidLeft", false },
        { "DualWield/ChainMidRightSwing", false, "Bas.Animation.Attack.DualWield.Counter.MidRight", false },
        { "DualWield/ChainLowLeftSwing", false, "Bas.Animation.Attack.DualWield.Counter.LowLeft", false },
        { "DualWield/ChainLowRightSwing", false, "Bas.Animation.Attack.DualWield.Counter.LowRight", false },
        { "DualWield/ChainThrustLeft", true, "Bas.Animation.Attack.DualWield.Counter.StabLeft", true },
        { "DualWield/ChainThrustRight", true, "Bas.Animation.Attack.DualWield.Counter.StabRight", true },
        { "DualWield/ChainHighLeftSwing", false, "Bas.Animation.Attack.DualWield.Counter.HighLeft", false },
        { "DualWield/ChainHighRightSwing", false, "Bas.Animation.Attack.DualWield.Counter.HighRight", false },
        { "DualWield/ChainCrossSlash", false, "Bas.Animation.Attack.DualWield.Counter.Cross", false },
        { "DualWield/RiposteHighRight", false, "Bas.Animation.Attack.DualWield.Riposte.HighRgt", false },
        { "DualWield/RiposteHighLeft", false, "Bas.Animation.Attack.DualWield.Riposte.HighLft", false },
        { "DualWield/RiposteRight", false, "Bas.Animation.Attack.DualWield.Riposte.Rgt", false },
        { "DualWield/RiposteLeft", false, "Bas.Animation.Attack.DualWield.Riposte.Lft", false },
        { "DualWield/RiposteLowRight", false, "Bas.Animation.Attack.DualWield.Riposte.LowRgt", false },
        { "DualWield/RiposteLowLeft", false, "Bas.Animation.Attack.DualWield.Riposte.LowLft", false },
        { "Shield/SwingLeftToRightLight", false, "Bas.Animation.Attack.Shield.Stance1.LtRLight", false },
        { "Shield/SwingRightToLeftLight", false, "Bas.Animation.Attack.Shield.Stance1.RtLLight", false },
        { "Shield/SwingLeftToRight", false, "Bas.Animation.Attack.Shield.Stance1.LtR", false },
        { "Shield/SwingRightToLeft", false, "Bas.Animation.Attack.Shield.Stance1.RtL", false },
        { "Shield/ThrustLight", true, "Bas.Animation.Attack.Shield.Stance2.StabLight", true },
        { "Shield/ThrustHeavy", true, "Bas.Animation.Attack.Shield.Stance2.StabHeavy", true },
        { "Shield/OverheardSwingLeftToRightLight", false, "Bas.Animation.Attack.Shield.Stance3.LtRLight", false },
        { "Shield/OverheardSwingRightToLeftLight", false, "Bas.Animation.Attack.Shield.Stance3.RtLLight", false },
        { "Shield/OverheardSwingLeftToRight", false, "Bas.Animation.Attack.Shield.Stance3.LtR", false },
        { "Shield/OverheardSwingRightToLeft", false, "Bas.Animation.Attack.Shield.Stance3.RtL", false },
        { "Shield/PushCloseLight", false, "Bas.Animation.Attack.Shield.PushCharge", false },
        { "Shield/PushMidAverage", false, "Bas.Animation.Attack.Shield.PushCharge", false },
        { "Shield/PushFarHeavy", false, "Bas.Animation.Attack.Shield.PushCharge", false },
        { "Shield/ChainPushMidAverage", false, "Bas.Animation.Attack.Shield.PushCharge", false },
        { "Shield/ChainPushFarHeavy", false, "Bas.Animation.Attack.Shield.PushCharge", false },
        { "Shield/RiposteRight", false, "Bas.Animation.Attack.Shield.Riposte.Rgt", false },
        { "Shield/RiposteLeft", false, "Bas.Animation.Attack.Shield.Riposte.Lft", false },
        { "Shield/RiposteHighRight", false, "Bas.Animation.Attack.Shield.Riposte.HighRgt", false },
        { "Shield/RiposteHighLeft", false, "Bas.Animation.Attack.Shield.Riposte.HighLft", false },
        { "Shield/RiposteLowRight", false, "Bas.Animation.Attack.Shield.Riposte.LowRgt", false },
        { "Shield/RiposteLowLeft", false, "Bas.Animation.Attack.Shield.Riposte.LowLft", false },
    };
}

public class ClashSlowMoVerdictTests
{
    private const float NpcReach = 2.5f;

    // User 2026-11-24: the clash's only condition is a two-handed block, so the slow-motion asks the same of the player:
    // the enemy within its swing reach and the player's weapon in both hands (Player.log 2026-11-23: the blade distance
    // refused 50 of 57 judged swings).
    [Fact]
    public void InReach_TwoHanded_Go() => Assert.Equal(SlowMoVerdict.Go, ClashRules.JudgeSlowMo(1.5f, NpcReach, twoHanded: true));

    [Fact]
    public void AtTheReachLimit_StillGo() => Assert.Equal(SlowMoVerdict.Go, ClashRules.JudgeSlowMo(NpcReach, NpcReach, twoHanded: true));

    [Fact]
    public void NpcFarAway_NoSlowMo() => Assert.Equal(SlowMoVerdict.NpcTooFar, ClashRules.JudgeSlowMo(2.6f, NpcReach, twoHanded: true));

    [Fact]
    public void OneHand_NoSlowMo() => Assert.Equal(SlowMoVerdict.OneHanded, ClashRules.JudgeSlowMo(1.5f, NpcReach, twoHanded: false));

    [Fact]
    public void NpcDistance_IsCheckedFirst() => Assert.Equal(SlowMoVerdict.NpcTooFar, ClashRules.JudgeSlowMo(9f, NpcReach, twoHanded: false));
}

// The push in a blade lock (user 2026-11-22: the duel was always lost; Player.log: leads of 0.12–0.24 m counted only
// 0.01–0.04 m, or even −0.17 m, because only the level direction to the enemy's body counted). The push now counts along
// the line from the player's head to the lock point: away from the player, wherever the blades crossed.
public class ClashPushTests
{
    private static readonly Vec3 Head = new Vec3(0, 1.7f, 0);

    [Fact]
    public void PushingAwayFromYou_TowardsTheLock_Counts()
    {
        var lockPoint = new Vec3(0, 1.4f, 0.6f);
        var away = new Vec3(0, -0.3f, 0.6f) * (0.1f / 0.6708204f);

        Assert.Equal(0.1f, ClashGeometry.PushLead(away, Head, lockPoint), 3);
    }

    [Fact]
    public void OverheadBind_PushingUp_Counts()
    {
        // An overhead strike blocked above the head: the blades cross straight above, pushing up is pushing.
        var lockPoint = new Vec3(0, 2.2f, 0.1f);

        Assert.True(ClashGeometry.PushLead(new Vec3(0, 0.1f, 0), Head, lockPoint) > 0.09f);
    }

    [Fact]
    public void SideBind_PushingSideways_Counts()
    {
        // A swing from the right blocked out to the right: the blades cross beside the player.
        var lockPoint = new Vec3(0.6f, 1.5f, 0.2f);

        Assert.True(ClashGeometry.PushLead(new Vec3(0.1f, 0, 0), Head, lockPoint) > 0.08f);
    }

    [Fact]
    public void PullingBack_CountsAgainst()
    {
        var lockPoint = new Vec3(0, 1.5f, 0.6f);

        Assert.True(ClashGeometry.PushLead(new Vec3(0, 0, -0.1f), Head, lockPoint) < 0f);
    }

    [Fact]
    public void LeadAcrossTheLine_CountsNothing()
    {
        var lockPoint = new Vec3(0, 1.7f, 0.6f);

        Assert.Equal(0f, ClashGeometry.PushLead(new Vec3(0.1f, 0, 0), Head, lockPoint), 4);
    }

    [Fact]
    public void LockAtTheHead_NoDirection_CountsNothing() =>
        Assert.Equal(0f, ClashGeometry.PushLead(new Vec3(0, 0, 0.1f), Head, Head));

    // User 2026-11-22: the push can also go sideways, right or left as the player sees it.
    private static readonly Vec3 HeadRight = new Vec3(1, 0, 0);

    [Fact]
    public void Components_StraightAway_IsAllForward()
    {
        var (forward, side) = ClashGeometry.PushComponents(new Vec3(0, 0, 0.1f), Head, new Vec3(0, 1.7f, 0.6f), HeadRight);

        Assert.Equal(0.1f, forward, 3);
        Assert.Equal(0f, side, 3);
    }

    [Fact]
    public void Components_ToTheRight_IsPositiveSide_ToTheLeftNegative()
    {
        var lockPoint = new Vec3(0, 1.5f, 0.6f);

        Assert.Equal(0.1f, ClashGeometry.PushComponents(new Vec3(0.1f, 0, 0), Head, lockPoint, HeadRight).Side, 3);
        Assert.Equal(-0.1f, ClashGeometry.PushComponents(new Vec3(-0.1f, 0, 0), Head, lockPoint, HeadRight).Side, 3);
    }

    [Fact]
    public void Components_OverheadBind_SideIsStillThePlayersRight()
    {
        // The blades cross straight above: forward is up, and right stays the player's right.
        var lockPoint = new Vec3(0, 2.3f, 0.05f);
        var (forward, side) = ClashGeometry.PushComponents(new Vec3(0.1f, 0, 0), Head, lockPoint, HeadRight);

        Assert.Equal(0.1f, side, 3);
        Assert.Equal(0f, forward, 2);
    }

    [Fact]
    public void Components_HeadRightTiltedIntoTheLine_IsMadeSquare()
    {
        // A head turned a little towards the lock: only the part of its right across the line counts as sideways.
        var tilted = new Vec3(0.8f, 0, 0.6f);
        var (forward, side) = ClashGeometry.PushComponents(new Vec3(0, 0, 0.1f), Head, new Vec3(0, 1.7f, 0.6f), tilted);

        Assert.Equal(0.1f, forward, 3);
        Assert.Equal(0f, side, 3);
    }
}

// What counts as pushing and which way the duel was won (user 2026-11-22: a sideways win always disarms).
public class ClashPushDirectionTests
{
    [Fact]
    public void Effective_IsForwardAndSideTogether_PullingBackAddsNothing()
    {
        Assert.Equal(0.1f, ClashPush.Effective(0.1f, 0f), 4);
        Assert.Equal(0.1f, ClashPush.Effective(0f, -0.1f), 4);
        Assert.Equal(0.05f, ClashPush.Effective(0.03f, 0.04f), 4);
        Assert.Equal(0.04f, ClashPush.Effective(-0.2f, 0.04f), 4);
    }

    [Theory]
    [InlineData(0.1f, 0f, false)]
    [InlineData(0.05f, 0.06f, true)]
    [InlineData(0.05f, -0.06f, true)]
    [InlineData(0.06f, 0.06f, false)]
    [InlineData(-0.1f, 0.02f, true)]
    [InlineData(0f, 0f, false)]
    public void Sideways_WhenTheSideIsTheBiggerPart(float forward, float side, bool sideways) =>
        Assert.Equal(sideways, ClashPush.IsSideways(forward, side));
}

public class ClashGeometryTests
{
    // User 2026-11-24 ("Controller-Weg"): the push is how far the player moved the real controllers since the blades
    // locked, the mean over every hand on the weapon. The in-game hand is no input any more: the 20x weight, the hold and
    // the frozen enemy blade moved it 0.1-0.5 m in the first second of every lock (harness and headset logs 2026-11-22/23),
    // and that read as a push the player never made (a lock won with the controllers standing still, harness 2026-11-24).
    [Fact]
    public void ControllerTravel_ControllersStandingStill_IsNoPush()
    {
        var starts = new[] { new Vec3(0.2f, 1.2f, 0.4f), new Vec3(-0.1f, 1.1f, 0.4f) };

        Assert.Equal(0f, ClashGeometry.ControllerTravel(starts, starts).Length, 4);
    }

    [Fact]
    public void ControllerTravel_BothControllersForward_IsThePush()
    {
        var starts = new[] { new Vec3(0.2f, 1.2f, 0.4f), new Vec3(-0.1f, 1.1f, 0.4f) };
        var nows = new[] { new Vec3(0.2f, 1.2f, 0.55f), new Vec3(-0.1f, 1.1f, 0.55f) };

        var travel = ClashGeometry.ControllerTravel(starts, nows);

        Assert.Equal(0.15f, travel.Z, 4);
        Assert.Equal(0f, travel.X, 4);
    }

    [Fact]
    public void ControllerTravel_OneControllerForward_CountsHalf()
    {
        var starts = new[] { new Vec3(0, 0, 0), new Vec3(0, 0, 0) };
        var nows = new[] { new Vec3(0, 0, 0.2f), new Vec3(0, 0, 0) };

        Assert.Equal(0.1f, ClashGeometry.ControllerTravel(starts, nows).Z, 4);
    }

    [Fact]
    public void ControllerTravel_ControllersMovingApart_Cancel()
    {
        var starts = new[] { new Vec3(0, 0, 0), new Vec3(0, 0, 0) };
        var nows = new[] { new Vec3(0.1f, 0, 0), new Vec3(-0.1f, 0, 0) };

        Assert.Equal(0f, ClashGeometry.ControllerTravel(starts, nows).Length, 4);
    }

    [Fact]
    public void ControllerTravel_NoController_IsZero()
    {
        Assert.Equal(0f, ClashGeometry.ControllerTravel(new Vec3[0], new Vec3[0]).Length, 4);
    }

    [Fact]
    public void ControllerTravel_UnevenLists_UseThePairsThatExist()
    {
        var starts = new[] { new Vec3(0, 0, 0), new Vec3(0, 0, 0) };
        var nows = new[] { new Vec3(0, 0, 0.3f) };

        Assert.Equal(0.3f, ClashGeometry.ControllerTravel(starts, nows).Z, 4);
    }

    [Fact]
    public void DistanceToSegment_ClampsToTheEnds()
    {
        var a = new Vec3(0, 0, 0);
        var b = new Vec3(1, 0, 0);

        Assert.Equal(0.5f, ClashGeometry.DistanceToSegment(new Vec3(0.5f, 0.5f, 0), a, b), 4);
        Assert.Equal(1f, ClashGeometry.DistanceToSegment(new Vec3(-1, 0, 0), a, b), 4);
        Assert.Equal(5f, ClashGeometry.DistanceToSegment(new Vec3(4, 4, 0), a, b), 4);
    }

    [Fact]
    public void DistanceToAZeroLengthSegment_IsToThePoint()
    {
        Assert.Equal(1f, ClashGeometry.DistanceToSegment(new Vec3(1, 0, 0), Vec3.Zero, Vec3.Zero), 4);
    }

    [Fact]
    public void MiddleOfTheBlade_IsTheMiddleOfItsExtent()
    {
        Assert.Equal(0.5f, ClashGeometry.MiddleOfExtent(new[] { 0.2f, 0.8f, 0.4f })!.Value, 4);
        Assert.Null(ClashGeometry.MiddleOfExtent(new float[0]));
    }
}

public class ClashTelegraphTests
{
    private const float Window = 0.1f;

    private static ClashTelegraph<int> Struck(float at = 10f)
    {
        var t = new ClashTelegraph<int>(Window);
        t.WindUp(1);
        t.StrikeStarted(1, at);
        return t;
    }

    [Fact]
    public void WindUpOfABigSwing_Flashes_ButIsNotJudgedYet()
    {
        var t = new ClashTelegraph<int>(Window);
        t.WindUp(1);

        Assert.True(t.IsFlashing(1));
        Assert.False(t.IsJudging(1, 10f));
    }

    [Fact]
    public void Strike_IsJudgedForTheWindowOnly()
    {
        var t = Struck(10f);

        Assert.True(t.IsJudging(1, 10f));
        Assert.True(t.IsJudging(1, 10.09f));
        Assert.False(t.IsJudging(1, 10.2f));
    }

    [Fact]
    public void AGoVerdict_StartsTheSlowMotion_Once()
    {
        var t = Struck();

        Assert.True(t.Verdict(1, SlowMoVerdict.Go, 10.02f));
        Assert.True(t.InSlowMo(1));
        Assert.False(t.IsJudging(1, 10.03f));
        Assert.False(t.Verdict(1, SlowMoVerdict.Go, 10.04f));
    }

    [Fact]
    public void ANoVerdict_KeepsJudgingWithinTheWindow_AndRemembersTheLastReason()
    {
        var t = Struck();

        Assert.False(t.Verdict(1, SlowMoVerdict.OneHanded, 10.01f));
        Assert.True(t.IsJudging(1, 10.05f));
        Assert.True(t.Verdict(1, SlowMoVerdict.Go, 10.05f));
    }

    [Fact]
    public void WindowOver_GivesTheLastReason_Once()
    {
        var t = Struck();
        t.Verdict(1, SlowMoVerdict.OneHanded, 10.01f);

        Assert.Equal(SlowMoVerdict.OneHanded, t.JudgingOver(1, 10.2f));
        Assert.Null(t.JudgingOver(1, 10.3f));
        Assert.False(t.IsJudging(1, 10.3f));
    }

    [Fact]
    public void WindowOverWithoutAnyVerdict_IsNotJudged()
    {
        Assert.Equal(SlowMoVerdict.NotJudged, Struck().JudgingOver(1, 10.2f));
    }

    [Fact]
    public void NoWindowOver_WhileStillJudging_OrInSlowMo()
    {
        var t = Struck();
        Assert.Null(t.JudgingOver(1, 10.05f));
        t.Verdict(1, SlowMoVerdict.Go, 10.05f);
        Assert.Null(t.JudgingOver(1, 10.5f));
    }

    [Fact]
    public void Ended_StopsTheFlash_AndSaysWhetherASlowMotionRan()
    {
        var t = Struck();
        t.Verdict(1, SlowMoVerdict.Go, 10.01f);

        Assert.True(t.Ended(1));
        Assert.False(t.IsFlashing(1));
        Assert.False(t.InSlowMo(1));
        Assert.False(t.Ended(1));
    }

    [Fact]
    public void StrikeOfAnAttackThatWasNotBig_IsIgnored()
    {
        var t = new ClashTelegraph<int>(Window);
        t.StrikeStarted(1, 10f);

        Assert.False(t.IsJudging(1, 10f));
        Assert.False(t.IsFlashing(1));
    }

    [Fact]
    public void OnlyOneSlowMotionAtATime()
    {
        var t = Struck();
        t.WindUp(2);
        t.StrikeStarted(2, 10f);
        t.Verdict(1, SlowMoVerdict.Go, 10.01f);

        Assert.False(t.Verdict(2, SlowMoVerdict.Go, 10.02f));
        Assert.True(t.AnySlowMo);
    }

    [Fact]
    public void ANewWindUp_StartsClean()
    {
        var t = Struck();
        t.Verdict(1, SlowMoVerdict.Go, 10.01f);
        t.Ended(1);
        t.WindUp(1);

        Assert.True(t.IsFlashing(1));
        Assert.False(t.InSlowMo(1));
    }

    [Fact]
    public void Clear_ListsWhoFlashed_AndForgetsAll()
    {
        var t = Struck();
        t.WindUp(2);

        Assert.Equal(new[] { 1, 2 }, t.Clear().OrderBy(i => i));
        Assert.False(t.IsFlashing(1));
        Assert.False(t.AnySlowMo);
    }
}

public class ClashSettingsTests
{
    [Fact]
    public void Defaults_AsDesigned()
    {
        Assert.True(ClashSettings.DefaultOnlySwords);
        Assert.True(ClashSettings.DefaultSlowMo);
        // 0.45 s since 2026-11-24 (headset logs: at 0.4 s two of three swings flashed; 0.45 keeps the lunge and overhead swings)
        Assert.Equal(0.45f, ClashSettings.MinWindUp.Default);
        Assert.Equal(0.3f, ClashSettings.SlowMoScale.Default);
        Assert.Equal(2.5f, ClashSettings.NpcReach.Default);
    }

    [Fact]
    public void FlashSounds_DefaultToTheSekiroLikeOne_AndNoGameSound()
    {
        Assert.Equal("DMCS 1", ClashSettings.DefaultFlashSound);
        Assert.Equal(FlashSound.Off, ClashSettings.DefaultFlashGameSound);
        Assert.Equal(0.8f, ClashSettings.FlashSoundVolume.Default);
    }

    [Theory]
    [InlineData("Metal Hit", "HitMaterialMetal")]
    [InlineData("Focus Ready", "FocusReady")]
    [InlineData("Gravity Pulse", "ImpactPulse")]
    [InlineData("Off", null)]
    [InlineData("nonsense", null)]
    public void GameSounds_MapToTheGamesEffects(string name, string effectId)
    {
        Assert.Equal(effectId, ClashSettings.GameSoundEffect(name));
    }

    [Fact]
    public void GameSounds_ListOffFirst()
    {
        Assert.Equal(new[] { "Off", "Metal Hit", "Focus Ready", "Gravity Pulse" }, ClashSettings.GameSounds);
    }

    [Fact]
    public void Defaults_AreOnTheirSliders()
    {
        foreach (var s in ClashSettings.Sliders)
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name);
        }
    }
}

public class GlintTests
{
    private const int Size = 128;

    private static float Alpha(byte[] px, int x, int y) => px[(((y * Size) + x) * 4) + 3] / 255f;

    [Fact]
    public void Glint_IsBrightInTheCentre_DarkInTheCorners()
    {
        var px = Glint.Pixels(Size);

        Assert.Equal(Size * Size * 4, px.Length);
        Assert.True(Alpha(px, Size / 2, Size / 2) > 0.95f);
        Assert.True(Alpha(px, 2, 2) < 0.02f);
    }

    [Fact]
    public void Glint_HasFourLongRays_AlongTheAxes()
    {
        var px = Glint.Pixels(Size);
        var c = Size / 2;
        var ray = Alpha(px, c, c - 40);
        var between = Alpha(px, c - 12, c - 40);

        Assert.True(ray > 0.35f, "ray " + ray);
        Assert.True(between < ray / 4f, "between " + between);
        Assert.Equal(Alpha(px, c, c - 40), Alpha(px, c - 40, c), 2);
    }

    [Fact]
    public void Glint_HasShorterFainterDiagonalRays()
    {
        var px = Glint.Pixels(Size);
        var c = Size / 2;
        var diagonal = Alpha(px, c - 18, c - 18);
        var main = Alpha(px, c, c - 25);
        var offDiagonal = Alpha(px, c - 18, c - 8);

        Assert.True(diagonal > 0.15f, "diagonal " + diagonal);
        Assert.True(diagonal < main, "diagonal " + diagonal + " main " + main);
        Assert.True(offDiagonal < diagonal, "off " + offDiagonal);
    }

    [Fact]
    public void Glint_HasASoftHalo_AroundTheCore()
    {
        var px = Glint.Pixels(Size);
        var c = Size / 2;

        Assert.True(Alpha(px, c - 10, c - 4) > 0.1f);
    }

    [Fact]
    public void Glint_IsWhite_SoTheMaterialColourTintsIt()
    {
        var px = Glint.Pixels(Size);
        var i = (((Size / 2) * Size) + (Size / 2)) * 4;

        Assert.Equal(255, px[i]);
        Assert.Equal(255, px[i + 1]);
        Assert.Equal(255, px[i + 2]);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.08f, 1f)]
    [InlineData(10f, 0.8f)]
    public void Pulse_FlashesThenHolds(float seconds, float expected)
    {
        Assert.Equal(expected, Glint.Pulse(seconds), 2);
    }

    [Theory]
    [InlineData(0f, 0.5f)]
    [InlineData(0.08f, 1.5f)]
    [InlineData(10f, 1f)]
    public void Scale_BurstsBigThenSettles(float seconds, float expected)
    {
        Assert.Equal(expected, Glint.Scale(seconds), 2);
    }

    [Fact]
    public void Spin_TurnsSlowly_ForATwinkle()
    {
        Assert.Equal(0f, Glint.SpinDegrees(0f));
        Assert.Equal(Glint.SpinDegreesPerSecond, Glint.SpinDegrees(1f), 2);
    }
}

public class ClashSlowMoOwnershipTests
{
    [Fact]
    public void Running_AtOurScale_IsOurs() => Assert.True(ClashRules.StillOurSlowMotion(starting: false, timeScale: 0.3f, ourScale: 0.3f));

    [Fact]
    public void Running_AtAnotherScale_IsSomeoneElses() => Assert.False(ClashRules.StillOurSlowMotion(false, 0.5f, 0.3f));

    [Fact]
    public void StillEasingIn_IsOurs()
    {
        // Found in game 2026-11-17: the swing ended 0.14 s after our slow-motion started, while the game still eased the
        // time scale down (0.68); comparing the scale alone left the slow-motion running for good.
        Assert.True(ClashRules.StillOurSlowMotion(starting: true, timeScale: 0.68f, ourScale: 0.3f));
    }

    [Fact]
    public void BackToNormalTime_IsNotOurs() => Assert.False(ClashRules.StillOurSlowMotion(false, 1f, 0.3f));
}

public class ClashSliderDefaultTests
{
    // The game finds a slider's default with Equals in its value list and falls back to the first value when it is not
    // there (SliderValues). 0.45 written as a literal is not 0.2 + 5 x 0.05 in float: the menu showed 0.2 (harness
    // step5a, 2026-11-24).
    [Fact]
    public void EveryClashSliderDefault_IsOneOfItsValues()
    {
        foreach (var s in ClashSettings.Sliders)
        {
            Assert.True(SliderValues.IndexOf(s.Values(), s.Default) >= 0, s.Name + " default " + s.Default + " is not one of its values");
        }
    }

    // The game (Mono) computed 0.2 + 5 x 0.05 to another float than the literal 0.45 (float arithmetic in double
    // precision), .NET 8 here does not, so the test above cannot catch it. The spec therefore takes its default from its
    // own value list, computed by the same code as the menu's values: an off-grid default lands on the nearest value.
    [Fact]
    public void ASliderSpec_SnapsItsDefaultToTheNearestValue()
    {
        var s = new SliderSpec("x", 0.2f, 0.8f, 0.05f, 0.46f);

        Assert.Equal(5, SliderValues.IndexOf(s.Values(), s.Default));
    }
}
