using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>The body the player has now; null while dead (see <see cref="PossessedBody"/>). Use instead of Player.currentCreature.</summary>
    internal static class CurrentPlayer
    {
        public static Creature Body
        {
            get
            {
                var creature = Player.currentCreature;
                var exists = creature != null;
                return PossessedBody.IsPlayer(exists, exists && creature.isPlayer) ? creature : null;
            }
        }
    }
}
