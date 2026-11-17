namespace DMCS.Core
{
    /// <summary>Which part of the player's body touched the NPC.</summary>
    public enum BodyPartKind
    {
        HandOrArm,
        FootOrLeg,

        /// <summary>Torso, head, neck.</summary>
        Other,
    }

    /// <summary>
    /// Whether a contact of the player's body with an NPC is the player's strike. The game reports any such contact as a
    /// hit by the player's part, also when the NPC ran into a motionless hand (harness 2026-11-17: a fighting NPC's swings
    /// into the standing player's hand read as punches, and Body Hit Drop disarmed it). The part's physics speed cannot tell
    /// the two apart (the hand moves because it was hit), so the hand's tracked controller speed decides. A kick counts as
    /// before (the legs are not tracked); torso and head never count.
    /// </summary>
    public static class PlayerStrikeRule
    {
        public const string MinHandSpeedName = "Body Hit Min Hand Speed";

        /// <summary>m/s the controller of the hitting hand must have reached shortly before the contact; 0 = every contact (a guess, headset).</summary>
        public static readonly SliderSpec MinHandSpeed = new SliderSpec(MinHandSpeedName, 0f, 5f, 0.25f, 1.5f);

        /// <summary>Seconds before the contact in which the hand's top speed counts (a punch slows on contact).</summary>
        public const float SpeedWindow = 0.2f;

        public static bool IsStrike(BodyPartKind part, float handSpeed, float minHandSpeed)
        {
            switch (part)
            {
                case BodyPartKind.FootOrLeg:
                    return true;
                case BodyPartKind.HandOrArm:
                    return handSpeed >= minHandSpeed;
                default:
                    return false;
            }
        }
    }
}
