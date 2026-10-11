using HarmonyLib;
using Dir = ControllerManager.Dir;
using Player = ControllerManager.ControllerPlayerNo;
using Prio = ControllerManager.Prio;
using LRType = ControllerManager.LRType;

namespace DfjkNav;

// Feeds the per-frame D/F/J/K intent into the game's own menu queries.
// High priority so the probe postfixes log the final value.
[HarmonyPatch(typeof(ControllerManager))]
[HarmonyPriority(Priority.High)]
internal static class InputPatches
{
    private static bool ForUs(Player p) => p == Player.Player1 || p == Player.All;

    private static void ApplyDir(Player p, ref Dir result)
    {
        if (result != Dir.None || !ForUs(p)) return;
        var d = Nav.Current.Dir;
        if (d != Dir.None) result = d;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetOkDown))]
    static void Ok(Player controllerPlayerNo, ref bool __result)
    {
        if (!__result && ForUs(controllerPlayerNo) && Nav.Current.Ok) __result = true;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetCancelDown))]
    static void Cancel(Player controllerPlayerNo, ref bool __result)
    {
        if (!__result && ForUs(controllerPlayerNo) && Nav.Current.Cancel) __result = true;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(bool))]
    static void DirDownA(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(LRType))]
    static void DirDownB(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), typeof(Player))]
    static void DirRepA(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), typeof(Player), typeof(LRType))]
    static void DirRepB(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ControllerManager.GetDirectionRepeat), new[] { typeof(Player), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Out })]
    static void DirRepC(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionRepeatTrigger), typeof(Player), typeof(Dir))]
    static void RepTrig(Player controllerPlayerNo, Dir dir, ref bool __result)
    {
        if (!__result && ForUs(controllerPlayerNo) && dir != Dir.None && Nav.Current.Dir == dir) __result = true;
    }
}
