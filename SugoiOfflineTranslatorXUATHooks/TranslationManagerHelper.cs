using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;
using HarmonyLib.Tools;
using XUnity.AutoTranslator.Plugin.Core;
using XUnity.AutoTranslator.Plugin.Core.Endpoints;

namespace SugoiOfflineTranslator.XUATHooks
{
    static class TranslationManagerHelper
    {
        // static FieldInfo field;
        static object currentTranslationManager;

        static PropertyInfo translationManagerCurrentEndpointManagerPropertyInfo;
        static PropertyInfo endpointPropertyInfo;
        
        static TranslationManagerHelper()
        {
            var translationManagerFieldInfo = AccessTools.Field(typeof(AutoTranslationPlugin), "TranslationManager");
            currentTranslationManager = translationManagerFieldInfo.GetValue(AutoTranslator.Default);

            var translationManagerType = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.TranslationManager");
            translationManagerCurrentEndpointManagerPropertyInfo = AccessTools.Property(translationManagerType, "CurrentEndpoint");

            var translationEndpointManagerType = AccessTools.TypeByName("XUnity.AutoTranslator.Plugin.Core.Endpoints.TranslationEndpointManager");

            endpointPropertyInfo = AccessTools.Property(translationEndpointManagerType, "Endpoint");
        }

        private static object CurrentEndpoingManager
        {
            get
            {
                return translationManagerCurrentEndpointManagerPropertyInfo.GetValue(currentTranslationManager, null);
            }

        }

        private static ITranslateEndpoint CurrentEndpoint
        {
            get
            {
                return endpointPropertyInfo.GetValue(CurrentEndpoingManager, null) as ITranslateEndpoint;
            }
        }

        public static bool IsSugoiTranslatorCurrentEndpoint => CurrentEndpoint.Id == "SugoiOfflineTranslator";
    }
}
