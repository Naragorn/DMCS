using DMCS.Core;
using Xunit;

namespace DMCS.Tests;

/// <summary>
/// After the player dies, the game releases the body (Player.ReleaseCreature: isPlayer false) but Player.currentCreature
/// keeps pointing at it until the next body. Windows run 2026-11-28: the corpse's hits counted as the player's for 8 s.
/// </summary>
public class PossessedBodyTests
{
    [Fact]
    public void The_possessed_body_is_the_player()
    {
        Assert.True(PossessedBody.IsPlayer(exists: true, possessed: true));
    }

    [Fact]
    public void A_released_body_is_not_the_player()
    {
        Assert.False(PossessedBody.IsPlayer(exists: true, possessed: false));
    }

    [Fact]
    public void No_body_is_not_the_player()
    {
        Assert.False(PossessedBody.IsPlayer(exists: false, possessed: false));
        Assert.False(PossessedBody.IsPlayer(exists: false, possessed: true));
    }
}
