namespace DMCS.Core
{
    /// <summary>What filled the meter (each has its own gain in the Mod Menu).</summary>
    public enum AdrenalineSource
    {
        Hit,
        BlockParry,
        DamageTaken,
        ChargeStagger,
        Disarm,
        Dodge,
        Catch,
        ClashWin,
    }

    /// <summary>Which game events count for the meter (plain values, read from the game by the caller).</summary>
    public static class AdrenalineSources
    {
        /// <summary>
        /// A hit (EventManager.onCreatureHit): the player hitting an NPC is a Hit; an NPC hit with damage on the
        /// player is DamageTaken. Only the hit's end counts, so each hit counts once.
        /// </summary>
        public static AdrenalineSource? FromHit(bool atEnd, HitSource source, bool targetIsPlayer, float damage)
        {
            if (!atEnd)
            {
                return null;
            }
            if (targetIsPlayer)
            {
                return source == HitSource.NotPlayer && damage > 0f ? AdrenalineSource.DamageTaken : (AdrenalineSource?)null;
            }
            return source != HitSource.NotPlayer ? AdrenalineSource.Hit : (AdrenalineSource?)null;
        }

        /// <summary>EventManager.onCreatureAttackParry: an NPC attack stopped by the player's weapon or shield.</summary>
        public static AdrenalineSource? FromParry(bool parriedIsPlayer, bool parryingIsPlayer)
        {
            return parryingIsPlayer && !parriedIsPlayer ? AdrenalineSource.BlockParry : (AdrenalineSource?)null;
        }
    }

    /// <summary>The gain per source, as set in the Mod Menu.</summary>
    public readonly struct AdrenalineGains
    {
        public AdrenalineGains(int hit, int blockParry, int damageTaken, int chargeStagger, int disarm, int dodge, int @catch, int clashWin = AdrenalineSettings.DefaultGainClashWin)
        {
            ClashWin = clashWin;
            Dodge = dodge;
            Catch = @catch;
            Hit = hit;
            BlockParry = blockParry;
            DamageTaken = damageTaken;
            ChargeStagger = chargeStagger;
            Disarm = disarm;
        }

        public int Hit { get; }
        public int BlockParry { get; }
        public int DamageTaken { get; }
        public int ChargeStagger { get; }
        public int Disarm { get; }
        public int Dodge { get; }
        public int Catch { get; }
        public int ClashWin { get; }

        public int For(AdrenalineSource source)
        {
            switch (source)
            {
                case AdrenalineSource.Hit: return Hit;
                case AdrenalineSource.BlockParry: return BlockParry;
                case AdrenalineSource.DamageTaken: return DamageTaken;
                case AdrenalineSource.ChargeStagger: return ChargeStagger;
                case AdrenalineSource.Dodge: return Dodge;
                case AdrenalineSource.Catch: return Catch;
                case AdrenalineSource.ClashWin: return ClashWin;
                default: return Disarm;
            }
        }
    }
}
