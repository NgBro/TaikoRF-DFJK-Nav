using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace DfjkNav;

[BepInPlugin(Guid, "DFJK Nav", "0.1.0")]
public class Plugin : BasePlugin
{
    public const string Guid = "dfjk.nav";

    internal static ManualLogSource L;
    internal static ConfigEntry<bool> Probe;

    public override void Load()
    {
        L = Log;
        Probe = Config.Bind("Debug", "Probe", true,
            "Log every ControllerManager query that returns input, and each new query signature per scene.");

        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        AddComponent<NavBehaviour>();
        L.LogInfo("DFJK Nav loaded");
    }
}
