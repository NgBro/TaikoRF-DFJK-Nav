using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.InputSystem;
using Dir = ControllerManager.Dir;
using Player = ControllerManager.ControllerPlayerNo;
using Prio = ControllerManager.Prio;
using LRType = ControllerManager.LRType;

namespace DfjkNav;

// Read-only instrumentation: which queries each scene makes, and which ones see input.
[HarmonyPatch(typeof(ControllerManager))]
internal static class ProbePatches
{
    private static readonly HashSet<string> SeenThisScene = new();
    private static readonly Dictionary<string, string> LastHeld = new();

    internal static void ResetScene()
    {
        SeenThisScene.Clear();
        LastHeld.Clear();
    }

    private static void Seen(string sig)
    {
        if (SeenThisScene.Add(sig))
            Plugin.L.LogInfo($"[seen] {NavBehaviour.Scene}: {sig}");
    }

    private static void Hit(string sig, object result)
    {
        Seen(sig);
        Plugin.L.LogInfo($"[hit] f{NavBehaviour.Frame} {NavBehaviour.Scene}: {sig} -> {result}");
    }

    private static void Report(string sig, Dir d)
    {
        if (!Plugin.Probe.Value) return;
        if (d == Dir.None) Seen(sig); else Hit(sig, d);
    }

    private static void Report(string sig, bool b)
    {
        if (!Plugin.Probe.Value) return;
        if (!b) Seen(sig); else Hit(sig, b);
    }

    // Held-style queries fire every frame while held; only log transitions.
    private static void ReportHeld(string sig, Dir d)
    {
        if (!Plugin.Probe.Value) return;
        Seen(sig);
        var s = d.ToString();
        if (LastHeld.TryGetValue(sig, out var prev) && prev == s) return;
        LastHeld[sig] = s;
        if (d != Dir.None) Plugin.L.LogInfo($"[held] f{NavBehaviour.Frame} {NavBehaviour.Scene}: {sig} -> {s}");
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(bool))]
    static void DirDownA(Player controllerPlayerNo, Prio prio, bool rightStick, Dir __result)
        => Report($"GetDirectionDown({controllerPlayerNo},{prio},rs={rightStick})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(LRType))]
    static void DirDownB(Player controllerPlayerNo, Prio prio, LRType LRType, Dir __result)
        => Report($"GetDirectionDown({controllerPlayerNo},{prio},lr={LRType})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionButton), typeof(Player), typeof(Prio), typeof(bool))]
    static void DirBtnA(Player controllerPlayerNo, Prio prio, bool rightStick, Dir __result)
        => ReportHeld($"GetDirectionButton({controllerPlayerNo},{prio},rs={rightStick})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionButton), typeof(Player), typeof(Prio), typeof(LRType))]
    static void DirBtnB(Player controllerPlayerNo, Prio prio, LRType LRType, Dir __result)
        => ReportHeld($"GetDirectionButton({controllerPlayerNo},{prio},lr={LRType})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), typeof(Player))]
    static void DirRepA(Player controllerPlayerNo, Dir __result)
        => Report($"GetDirectionRepeat({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), typeof(Player), typeof(LRType))]
    static void DirRepB(Player controllerPlayerNo, LRType LRType, Dir __result)
        => Report($"GetDirectionRepeat({controllerPlayerNo},lr={LRType})", __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), new[] { typeof(Player), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Out })]
    static void DirRepC(Player controllerPlayerNo, Dir __result)
        => Report($"GetDirectionRepeat({controllerPlayerNo},out hs)", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeatTrigger), typeof(Player), typeof(Dir))]
    static void RepTrigA(Player controllerPlayerNo, Dir dir, bool __result)
        => Report($"GetDirectionRepeatTrigger({controllerPlayerNo},{dir})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetOkDown))]
    static void Ok(Player controllerPlayerNo, bool __result) => Report($"GetOkDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetCancelDown))]
    static void Cancel(Player controllerPlayerNo, bool __result) => Report($"GetCancelDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetMenuDown))]
    static void Menu(Player controllerPlayerNo, bool __result) => Report($"GetMenuDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetViewDown))]
    static void View(Player controllerPlayerNo, bool __result) => Report($"GetViewDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonXDown))]
    static void X(Player controllerPlayerNo, bool __result) => Report($"GetButtonXDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonYDown))]
    static void Y(Player controllerPlayerNo, bool __result) => Report($"GetButtonYDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonLBDown))]
    static void LB(Player controllerPlayerNo, bool __result) => Report($"GetButtonLBDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonRBDown))]
    static void RB(Player controllerPlayerNo, bool __result) => Report($"GetButtonRBDown({controllerPlayerNo})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonDown))]
    static void BtnDown(Player controllerPlayerNo, ControllerManager.Buttons button, bool __result)
        => Report($"GetButtonDown({controllerPlayerNo},{button})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetKeyDown))]
    static void KeyDown(Player controllerPlayerNo, Key key, bool __result)
        => Report($"GetKeyDown({controllerPlayerNo},{key})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDonKatsuDown))]
    static void DonKatsu(Player controllerPlayerNo, ControllerManager.Taiko taiko, bool __result)
        => Report($"GetDonKatsuDown({controllerPlayerNo},{taiko})", __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetMouseButtonDown))]
    static void Mouse(ControllerManager.MouseButton button, bool __result)
        => Report($"GetMouseButtonDown({button})", __result);
}

// Read-only: confirms drum hits still reach gameplay with the mod installed.
[HarmonyPatch(typeof(EnsoInput))]
internal static class EnsoProbe
{
    [HarmonyPostfix, HarmonyPatch(nameof(EnsoInput.UpdateController))]
    static void UpdateController(int player, EnsoInput.EnsoInputFlag __result)
    {
        if (__result != 0) Plugin.L.LogInfo($"[enso] f{NavBehaviour.Frame} p{player} {__result}");
    }
}

