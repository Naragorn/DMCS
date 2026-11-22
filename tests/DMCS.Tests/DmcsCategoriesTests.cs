using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

public class DmcsCategoriesTests
{
    [Theory]
    [InlineData(DmcsCategories.General, 0)]
    [InlineData(DmcsCategories.ClashOfMight, 1)]
    [InlineData(DmcsCategories.DisarmStrike, 2)]
    [InlineData(DmcsCategories.ChargeStagger, 3)]
    [InlineData(DmcsCategories.Adrenaline, 4)]
    [InlineData(DmcsCategories.AdrenalineMagic, 5)]
    [InlineData(DmcsCategories.Parry, 6)]
    [InlineData(DmcsCategories.SkillTree, 7)]
    [InlineData("Unknown", -1)]
    public void OrderOf(string category, int expected)
    {
        Assert.Equal(expected, DmcsCategories.OrderOf(category));
    }

    [Fact]
    public void Features_FollowSpecOrder()
    {
        Assert.Equal(
            new[] { "Clash of Might", "Disarm Strike", "Charge Stagger", "Adrenaline", "Parry" },
            DmcsCategories.Features);
    }
}
