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
    public partial class SugoiOfflineTranslatorXUATHooksPlugin
    {
        public static SugoiOfflineTranslatorXUATHooksPlugin Instance { get; private set; }

        Harmony harmonyInstance;

        public void Awake()
        {
            this.Init();

        }

        protected void Init()
        {
            if (Instance != null)
            {
                throw new Exception("Already initialized");
            }

            Instance = this;
            harmonyInstance = new Harmony("org.bepinex.plugins.sugoiofflinetranslator.xuathooks");
            LogDebug("Initialized");
            this.OnEnable();
        }

        public void OnEnable()
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
