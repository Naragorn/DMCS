using DMCS.Core;
using ThunderRoad;

namespace DMCS
{
    /// <summary>
    /// Entry point. ThunderRoad instantiates every ThunderScript subclass in the mod DLL
    /// (ModManager.LoadModThunderScripts: Activator.CreateInstance -> ScriptLoaded -> Enable).
    /// Order in ModManager.LoadCoroutine / LoadAndRefreshCatalog: options are discovered
    /// (LoadModOptions), then this script loads, and only then are the saved option values
    /// applied (Options.ApplyModOptions). So at ScriptLoaded the options still hold their defaults;
    /// the effective settings are logged when a level has loaded.
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
                + modData.modOptions.Count + " mod options. Saved settings are applied after loading.");
        }

        public override void ScriptEnable()
        {
            EventManager.onLevelLoad += OnLevelLoad;
            EventManager.onLevelUnload += OnLevelUnload;
            ChargeStagger.Start();
            DisarmStrike.Start();
            Adrenaline.Start();
            ClashOfMight.Start();
            // After Clash of Might: its parry handler must run first (Parry.cs).
            Parry.Start();
            Finisher.Start();
            SkillGate.Start();
            Dmcs.Log.Debug("Script", "ScriptEnable");
        }

        public override void ScriptDisable()
        {
            EventManager.onLevelLoad -= OnLevelLoad;
            EventManager.onLevelUnload -= OnLevelUnload;
            ChargeStagger.Stop();
            DisarmStrike.Stop();
            Adrenaline.Stop();
            ClashOfMight.Stop();
            Parry.Stop();
            Finisher.Stop();
            SkillGate.Stop();
            Dmcs.Log.Debug("Script", "ScriptDisable");
        }

        public override void ScriptUnload()
        {
            EventManager.onLevelLoad -= OnLevelLoad;
            EventManager.onLevelUnload -= OnLevelUnload;
            ChargeStagger.Stop();
            DisarmStrike.Stop();
            Adrenaline.Stop();
            ClashOfMight.Stop();
            Parry.Stop();
            Finisher.Stop();
            SkillGate.Stop();
            Dmcs.Log.Debug("Script", "ScriptUnload");
            LoadedMod = null;
        }

        public override void ScriptUpdate()
        {
            HandSpeeds.Update();
            ChargeStagger.Update();
            DisarmStrike.Update();
            Adrenaline.Update();
            ClashOfMight.Update();
            Parry.Update();
            HitInfo.EndFrame();
        }

        public override void ScriptFixedUpdate()
        {
            ClashOfMight.FixedUpdate();
        }

        private static void OnLevelUnload(LevelData levelData, LevelData.Mode mode, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart)
            {
                ChargeStagger.ReleaseAll();
                DisarmStrike.RestoreAll();
                Adrenaline.ResetAll();
                ClashOfMight.ResetAll();
                Parry.ResetAll();
            }
        }

        private static void OnLevelLoad(LevelData levelData, LevelData.Mode mode, EventTime eventTime)
        {
            if (eventTime != EventTime.OnEnd)
            {
                return;
            }
            SkillGate.OnLevelLoaded();
            Dmcs.Log.Info(DmcsStatus.Describe(Dmcs.Log.DebugEnabled, Features.All));
            AdrenalineSignals.Prewarm();
            ClashOfMight.Prewarm();
        }
    }
}
