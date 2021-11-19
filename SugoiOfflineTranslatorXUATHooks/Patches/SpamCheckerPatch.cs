using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using HarmonyLib.Tools;

namespace SugoiOfflineTranslator.XUATHooks.Patches
{
    [HarmonyPatch]
    static class SpamCheckerPatch
    {
        static MethodBase TargetMethod()
        {
            var targetClass = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.SpamChecker");
            return AccessTools.Method(targetClass, "PerformChecks");
        }

        static bool Prefix()
        {
            if (!TranslationManagerHelper.IsSugoiTranslatorCurrentEndpoint)
            {
                return true;
            }

#if DEBUG            
            SugoiOfflineTranslatorXUATHooksPlugin.Log("Spam checker call ignored");
#endif
            return false;
        }
    }
}
