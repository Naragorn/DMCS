using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>Gains from other mods (Adrenaline.AddExternal): every flow of the rule and of the meter it feeds.</summary>
public class AdrenalineExternalTests
{
    [Theory]
    [InlineData(false, 10f, false)]
    [InlineData(true, 0f, false)]
    [InlineData(true, -5f, false)]
    [InlineData(true, float.NaN, false)]
    [InlineData(true, float.PositiveInfinity, false)]
    [InlineData(true, 0.5f, true)]
    [InlineData(true, 25f, true)]
    public void Accepts_OnlyAPositiveAmount_WhileOn(bool enabled, float amount, bool expected)
    {
        Assert.Equal(expected, AdrenalineExternal.Accepts(enabled, amount));
    }

    [Theory]
    [InlineData("AchillesHeelCut", "External:AchillesHeelCut")]
    [InlineData("AchillesCrippledKill", "External:AchillesCrippledKill")]
    [InlineData("", "External:?")]
    [InlineData(null, "External:?")]
    public void Label_NamesTheSource(string source, string expected)
    {
        Assert.Equal(expected, AdrenalineExternal.Label(source));
    }

    [Fact]
    public void Gain_Fills_ThenOpensTheWindow_ThenIsRefusedWhileReady()
    {
        var m = new AdrenalineMeter();

        Assert.Equal(AdrenalineEvent.Gained, m.Gain(40f, 0f, 100f, 6f));
        Assert.Equal(AdrenalineEvent.Filled, m.Gain(60f, 1f, 100f, 6f));
        Assert.Equal(AdrenalineState.Ready, m.State);
        Assert.Equal(AdrenalineEvent.None, m.Gain(10f, 2f, 100f, 6f));
    }
}
