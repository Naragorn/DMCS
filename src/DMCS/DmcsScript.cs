using ThunderRoad;

namespace DMCS
{
    /// <summary>
    /// Entry point. ThunderRoad instantiates every ThunderScript subclass in the mod DLL
    /// (ModManager.LoadModThunderScripts: Activator.CreateInstance -> ScriptLoaded -> Enable).
    /// Mod options are discovered and applied by the game before this runs.
    /// </summary>
    public sealed class DmcsScript : ThunderScript
    {
        /// <summary>The loaded mod's data (holds the ModOption list used by Reset To Defaults).</summary>
        public static ModManager.ModData LoadedMod { get; private set; }

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            LoadedMod = modData;
            Dmcs.Log.Info(
                "v" + Dmcs.Version + " loaded from folder '" + modData.folderName + "' with "
                + modData.modOptions.Count + " mod options. Debug Logging="
                + (Dmcs.Log.DebugEnabled ? "on" : "off"));
            foreach (var feature in Features.All)
            {
                Dmcs.Log.Info(feature.Name + ": " + (feature.Enabled ? "Enabled" : "Disabled"));
            }
        }

        public override void ScriptEnable()
        {
            Dmcs.Log.Debug("Script", "ScriptEnable");
        }

        public override void ScriptDisable()
        {
            Dmcs.Log.Debug("Script", "ScriptDisable");
        }

        public override void ScriptUnload()
        {
            Dmcs.Log.Debug("Script", "ScriptUnload");
            LoadedMod = null;
        }
    }
}
