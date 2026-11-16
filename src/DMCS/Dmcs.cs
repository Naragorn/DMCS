using DMCS.Core;

namespace DMCS
{
    /// <summary>Process-wide mod state shared by all features.</summary>
    public static class Dmcs
    {
        public const string Version = "0.1.0";

        /// <summary>Writes to the Unity log (Player.log / in-game console).</summary>
        public static readonly DmcsLog Log = new DmcsLog(UnityEngine.Debug.Log);
    }
}
