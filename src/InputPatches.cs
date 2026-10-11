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
        if (!ForUs(p)) return;
        var it = Nav.Current;
        if (it.Dir != Dir.None) result = it.Dir;
        else if (it.Suppress) result = Dir.None;
    }

    private static void ApplyBool(Player p, ref bool result, bool ours)
    {
        if (!ForUs(p)) return;
        if (ours) result = true;
        else if (Nav.Current.Suppress) result = false;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetOkDown))]
    static void Ok(Player controllerPlayerNo, ref bool __result)
    {
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.Ok);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetCancelDown))]
    static void Cancel(Player controllerPlayerNo, ref bool __result)
    {
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.Cancel);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonXDown))]
    static void X(Player controllerPlayerNo, ref bool __result)
    {
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.X);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonYDown))]
    static void Y(Player controllerPlayerNo, ref bool __result)
    {
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.Y);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetMenuDown))]
    static void Menu(Player controllerPlayerNo, ref bool __result)
    {
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.Menu);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonLBDown))]
    static void LB(Player controllerPlayerNo, ref bool __result)
    {
        QueryTracker.Asked(QueryTracker.Tab);
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.TabPrev);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonRBDown))]
    static void RB(Player controllerPlayerNo, ref bool __result)
    {
        QueryTracker.Asked(QueryTracker.Tab);
        ApplyBool(controllerPlayerNo, ref __result, Nav.Current.TabNext);
    }

    // Raw button queries some screens use directly; only suppress the game's own D/F/J/K meanings here.
    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButtonDown))]
    static void BtnDown(Player controllerPlayerNo, ref bool __result)
    {
        if (__result && ForUs(controllerPlayerNo) && Nav.Current.Suppress) __result = false;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetButton))]
    static void Btn(Player controllerPlayerNo, ref bool __result)
    {
        if (__result && ForUs(controllerPlayerNo) && Nav.Current.Suppress) __result = false;
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionButton), typeof(Player), typeof(Prio), typeof(bool))]
    static void DirBtnA(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionButton), typeof(Player), typeof(Prio), typeof(LRType))]
    static void DirBtnB(Player controllerPlayerNo, ref Dir __result) => ApplyDir(controllerPlayerNo, ref __result);

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(bool))]
    static void DirDownA(Player controllerPlayerNo, ref Dir __result)
    {
        QueryTracker.AskedDir(LRType.None);
        ApplyDir(controllerPlayerNo, ref __result);
    }

    [HarmonyPostfix, HarmonyPatch(nameof(ControllerManager.GetDirectionDown), typeof(Player), typeof(Prio), typeof(LRType))]
    static void DirDownB(Player controllerPlayerNo, LRType LRType, ref Dir __result)
    {
        QueryTracker.AskedDir(LRType);
        ApplyDir(controllerPlayerNo, ref __result);
    }

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
        ApplyBool(controllerPlayerNo, ref __result, dir != Dir.None && Nav.Current.Dir == dir);
    }
}
