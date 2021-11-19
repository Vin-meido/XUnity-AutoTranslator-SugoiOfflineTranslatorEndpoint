using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using BepInEx;
using HarmonyLib;
using SugoiOfflineTranslator.XUATHooks.Patches;


namespace SugoiOfflineTranslator.XUATHooks
{
    [BepInPlugin("org.bepinex.plugins.sugoiofflinetranslator.xuathooks", "SugoiOfflineTranslaotXUATHooks", "1.0.0.0")]
    public class SugoiOfflineTranslatorXUATHooksPlugin : BaseUnityPlugin
    {
        public static SugoiOfflineTranslatorXUATHooksPlugin Instance { get; private set; }

        Harmony harmonyInstance;

        public void Awake()
        {
            if (Instance != null)
            {
                throw new Exception("Already initialized");
            }

            Instance = this;
            harmonyInstance = new Harmony("org.bepinex.plugins.sugoiofflinetranslator.xuathooks");
        }

        public void OnEnable()
        {
#if DEBUG
            Log("Enabling patches");
#endif
            harmonyInstance.PatchAll(typeof(AutoTranslationPluginPatch));
            harmonyInstance.PatchAll(typeof(SpamCheckerPatch));
        }

        public void OnDisable()
        {
            harmonyInstance.UnpatchSelf();
        }

        internal static void Log(object obj)
        {
#if DEBUG
            Instance.Logger.LogDebug(obj);
#endif
        }
    }
}
