
using BepInEx;


namespace SugoiOfflineTranslator.XUATHooks
{
    partial class XUATHooksPlugin : BaseUnityPlugin
    {
        public void Awake()
        {
            this.Init();
        }

        void OnEnable()
        {
            this.PatchAll();
        }

        internal static void LogDebug(object obj)
        {
#if DEBUG
            Instance.Log.LogDebug(obj);
#endif
        }

    }
}
