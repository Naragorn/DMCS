namespace DMCS.Core
{
    /// <summary>Rules for gains other mods add through <c>DMCS.Adrenaline.AddExternal(float amount, string source)</c>.</summary>
    public static class AdrenalineExternal
    {
        /// <summary>Only while Adrenaline is on, and only a positive amount.</summary>
        public static bool Accepts(bool enabled, float amount) => enabled && amount > 0f && !float.IsNaN(amount) && !float.IsInfinity(amount);

        /// <summary>How the gain shows in the debug log: "External:&lt;source&gt;".</summary>
        public static string Label(string source) => "External:" + (string.IsNullOrEmpty(source) ? "?" : source);
    }
}
