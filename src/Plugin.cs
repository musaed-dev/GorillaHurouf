using BepInEx;

namespace GorillaHurouf
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            Logger.LogInfo(PluginInfo.NAME + " " + PluginInfo.Version + " loaded");
            gameObject.AddComponent<NotificationPanel>();   // draws what Arabic.Notify queues; makes nothing until the first notification
            gameObject.AddComponent<MessageOfTheDay>();     // does nothing until Arabic.SetMessageOfTheDay is called
#if HUROUF_DEMO
            gameObject.AddComponent<Demo>();
#endif
        }

        // A reload (ScriptEngine) must leave nothing behind: the fallback entries and the font asset go.
        void OnDestroy() { Arabic.Cleanup(); }
    }
}
