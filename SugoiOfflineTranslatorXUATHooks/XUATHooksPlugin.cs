using System;

using BepInEx;
using HarmonyLib;
using SugoiOfflineTranslator.XUATHooks.Patches;


namespace SugoiOfflineTranslator.XUATHooks
{
    [BepInPlugin("org.bepinex.plugins.sugoiofflinetranslator.xuathooks", "SugoiOfflineTranslaotXUATHooks", "1.0.0.0")]
    public partial class XUATHooksPlugin
    {
        public static XUATHooksPlugin Instance { get; private set; }

        Harmony harmonyInstance;

        protected void Init()
        {
            if (Instance != null)
            {
                throw new Exception("Already initialized");
            }

            Instance = this;
            harmonyInstance = new Harmony("org.bepinex.plugins.sugoiofflinetranslator.xuathooks");
            LogDebug("Initialized");
        }

        public void PatchAll()
        {
#if DEBUG
            LogDebug("Enabling patches");
#endif
            harmonyInstance.PatchAll(typeof(AutoTranslationPluginPatch));
            harmonyInstance.PatchAll(typeof(SpamCheckerPatch));
        }

        public void OnDisable()
        {
            harmonyInstance.UnpatchSelf();
        }
    }
}
