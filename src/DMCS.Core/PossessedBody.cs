namespace DMCS.Core
{
    /// <summary>
    /// Whether the game's current player body is still the player. Player.ReleaseCreature (the player died) clears
    /// Creature.isPlayer but leaves Player.currentCreature on the corpse until the next body is taken (ThunderRoad 1.3.2).
    /// Windows run 2026-11-28: for 8 s after the death, the corpse's hits fed the adrenaline meter and Charge Stagger.
    /// </summary>
    public static class PossessedBody
    {
        public static bool IsPlayer(bool exists, bool possessed) => exists && possessed;
    }
}
