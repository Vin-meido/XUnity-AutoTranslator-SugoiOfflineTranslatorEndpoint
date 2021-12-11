using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using XUnity.AutoTranslator.Plugin.Core;

namespace SugoiOfflineTranslator.XUATHooks.Patches
{
    [HarmonyPatch]
    [HarmonyPatch(typeof(AutoTranslationPlugin))]
    [HarmonyPatch("WaitForTextStablization")]
    [HarmonyPatch(new Type[] {
            typeof(object),
            typeof(float),
            typeof(int),
            typeof(int),
            typeof(Action<string>),
            typeof(Action)
        })]
    class AutoTranslationPluginPatch
    {
        static void Prefix(object ui, ref float delay, int maxTries, int currentTries, Action<string> onTextStabilized, Action onMaxTriesExceeded)
        {
            if (!TranslationManagerHelper.IsSugoiTranslatorCurrentEndpoint)
            {
                return;
            }

# if DEBUG
            SugoiOfflineTranslatorXUATHooksPlugin.LogDebug("WaitForTextStabilization override duration");
# endif
            delay = 0.1f;
        }
    }
}
