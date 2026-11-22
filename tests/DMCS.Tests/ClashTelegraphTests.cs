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

public class ClashSlowMoVerdictTests
{
    private const float NpcReach = 2.5f;
    private const float BlockReach = 0.6f;
    private const float Tolerance = 45f;

    private static SlowMoVerdict Judge(float npc = 1.5f, float blade = 0.3f, float? angle = 10f) =>
        ClashRules.JudgeSlowMo(npc, NpcReach, blade, BlockReach, angle, Tolerance);

    [Fact]
    public void AllConditionsHold_Go() => Assert.Equal(SlowMoVerdict.Go, Judge());

    [Fact]
    public void AtTheLimits_StillGo() => Assert.Equal(SlowMoVerdict.Go, Judge(NpcReach, BlockReach, Tolerance));

    [Fact]
    public void NpcFarAway_NoSlowMo() => Assert.Equal(SlowMoVerdict.NpcTooFar, Judge(npc: 2.6f));

    [Fact]
    public void BladeFarFromTheFlash_NoSlowMo() => Assert.Equal(SlowMoVerdict.BladeTooFar, Judge(blade: 0.7f));

    [Fact]
    public void NoBladeInHand_IsTooFar() => Assert.Equal(SlowMoVerdict.BladeTooFar, Judge(blade: float.PositiveInfinity));

    [Fact]
    public void SwingPlaneNotKnown_NoSlowMo() => Assert.Equal(SlowMoVerdict.NoSwingPlane, Judge(angle: null));

    [Fact]
    public void BladeNotAcrossTheSwing_NoSlowMo() => Assert.Equal(SlowMoVerdict.WrongAngle, Judge(angle: 46f));

    [Fact]
    public void NpcDistance_IsCheckedFirst() => Assert.Equal(SlowMoVerdict.NpcTooFar, Judge(npc: 9f, blade: 9f, angle: 80f));
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
    private static readonly Vec3 Up = new Vec3(0, 1, 0);
    private static readonly Vec3 Right = new Vec3(1, 0, 0);
    private static readonly Vec3 Forward = new Vec3(0, 0, 1);

    [Fact]
    public void OverheadChop_PlaneNormalIsSideways()
    {
        // Blade points forward, moves down: the swing plane is the vertical forward plane, its normal sideways.
        var normal = ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, -3, 0), 0.3f);

        Assert.NotNull(normal);
        Assert.Equal(1f, System.Math.Abs(normal!.Value.X), 3);
    }

    [Fact]
    public void TooSlowBlade_HasNoPlane()
    {
        Assert.Null(ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, -0.2f, 0), 0.3f));
    }

    [Fact]
    public void BladeMovingAlongItself_HasNoPlane()
    {
        Assert.Null(ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, 0.1f, 3f), 0.3f));
    }

    [Fact]
    public void HorizontalBlockAgainstAnOverheadChop_IsARightAngle()
    {
        var normal = ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, -3, 0), 0.3f)!.Value;

        Assert.Equal(0f, ClashGeometry.AngleToNormal(Right, normal), 2);
        Assert.Equal(0f, ClashGeometry.AngleToNormal(new Vec3(-1, 0, 0), normal), 2);
    }

    [Fact]
    public void BladeParallelToTheSwing_IsNinetyDegreesOff()
    {
        var normal = ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, -3, 0), 0.3f)!.Value;

        Assert.Equal(90f, ClashGeometry.AngleToNormal(Up, normal), 2);
        Assert.Equal(90f, ClashGeometry.AngleToNormal(Forward, normal), 2);
    }

    [Fact]
    public void DiagonalBlade_IsFortyFiveOff()
    {
        var normal = ClashGeometry.SwingPlaneNormal(Forward, new Vec3(0, -3, 0), 0.3f)!.Value;

        Assert.Equal(45f, ClashGeometry.AngleToNormal(new Vec3(1, 1, 0), normal), 2);
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

        Assert.False(t.Verdict(1, SlowMoVerdict.WrongAngle, 10.01f));
        Assert.True(t.IsJudging(1, 10.05f));
        Assert.True(t.Verdict(1, SlowMoVerdict.Go, 10.05f));
    }

    [Fact]
    public void WindowOver_GivesTheLastReason_Once()
    {
        var t = Struck();
        t.Verdict(1, SlowMoVerdict.BladeTooFar, 10.01f);

        Assert.Equal(SlowMoVerdict.BladeTooFar, t.JudgingOver(1, 10.2f));
        Assert.Null(t.JudgingOver(1, 10.3f));
        Assert.False(t.IsJudging(1, 10.3f));
    }

    [Fact]
    public void WindowOverWithoutAnyVerdict_IsNoSwingPlane()
    {
        Assert.Equal(SlowMoVerdict.NoSwingPlane, Struck().JudgingOver(1, 10.2f));
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
        Assert.Equal(0.4f, ClashSettings.MinWindUp.Default);
        Assert.Equal(0.3f, ClashSettings.SlowMoScale.Default);
        Assert.Equal(90f, ClashSettings.BlockAngleTolerance.Default);
        // Headset 2026-11-22: the slow-motion hardly came (BladeTooFar on 54 of 63 swings at 0.6 m). 1.0 m is a guess.
        Assert.Equal(1.0f, ClashSettings.BlockReach.Default);
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
