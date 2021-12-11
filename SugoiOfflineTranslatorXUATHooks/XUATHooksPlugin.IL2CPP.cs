using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.IL2CPP;

namespace SugoiOfflineTranslator.XUATHooks
{
    partial class SugoiOfflineTranslatorXUATHooksPlugin : BasePlugin
    {
        public override void Load()
        {
            this.Init();
        }

        internal static void LogDebug(object obj)
        {
#if DEBUG
            Instance.Log.LogDebug(obj);
#endif
        }

    }
}
