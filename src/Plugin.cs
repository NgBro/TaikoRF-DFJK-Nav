using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace DfjkNav;

[BepInPlugin(Guid, "DFJK Nav", "0.1.0")]
public class Plugin : BasePlugin
{
    public const string Guid = "dfjk.nav";

    internal static ManualLogSource L;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<bool> Probe;
    internal static ConfigEntry<float> DoubleTapWindow;

    // Menu keys (also the default drum keys: D = left rim, F/J = face, K = right rim).
    internal static Key PrevKey = Key.D, NextKey = Key.K, ConfirmKey = Key.J, BackKey = Key.F;

    /// <summary>Chatty navigation trace; only written when Debug.Probe is on.</summary>
    internal static void Dbg(string msg)
    {
        if (Probe.Value) L.LogInfo(msg);
    }

    public override void Load()
    {
        L = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Navigate menus with the four drum keys. Gameplay input is never changed.");
        PrevKey = BindKey("Previous", Key.D, "Moves focus to the previous item (hold to keep moving).");
        NextKey = BindKey("Next", Key.K, "Moves focus to the next item (hold to keep moving).");
        ConfirmKey = BindKey("Confirm", Key.J, "Confirms the focused item.");
        BackKey = BindKey("Back", Key.F, "Goes back (same as the on-screen Back slot); while editing a value, finishes editing.");
        if (BackKey == ConfirmKey)
        {
            L.LogWarning($"Keys.Back and Keys.Confirm are both {ConfirmKey}; using {ConfirmKey} as confirm only");
            BackKey = Key.None;
        }
        DoubleTapWindow = Config.Bind("General", "DoubleTapWindow", 0.12f, new ConfigDescription(
            "Seconds within which a second D/K tap counts as a double tap (page skip on the song list). " +
            "On that list a single tap waits this long before moving. 0.12 = about 8 taps per second.",
            new AcceptableValueRange<float>(0.05f, 0.5f)));
        Probe = Config.Bind("Debug", "Probe", false,
            "Log navigation decisions, every input query that returns input, and drum hits during songs. " +
            "Diagnostics only; leave off for normal play.");

        if (!Enabled.Value) { L.LogInfo("DFJK Nav disabled in config"); return; }

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(InputPatches));
        if (Probe.Value)
        {
            // Diagnostics only: they run on every input query, so keep them off for normal play.
            harmony.PatchAll(typeof(ProbePatches));
            harmony.PatchAll(typeof(EnsoProbe));
        }
        AddComponent<NavBehaviour>();
        L.LogInfo($"Keys: previous={PrevKey} next={NextKey} confirm={ConfirmKey} back={BackKey}");
        L.LogInfo("DFJK Nav loaded");
    }

    private Key BindKey(string name, Key def, string desc)
    {
        var e = Config.Bind("Keys", name, def.ToString(), desc + " A Unity Input System key name (e.g. D, K, F, J, Space).");
        if (Enum.TryParse<Key>(e.Value, true, out var k) && k != Key.None) return k;
        L.LogWarning($"Keys.{name} = \"{e.Value}\" is not a key name; using {def}");
        return def;
    }
}
