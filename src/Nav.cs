using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Dir = ControllerManager.Dir;

namespace DfjkNav;

/// <summary>
/// Turns D/F/J/K into menu intents, once per frame, the first time anyone asks
/// (our Update or one of the game's ControllerManager queries), so the order of
/// MonoBehaviour updates doesn't matter.
/// </summary>
internal static class Nav
{
    internal struct Intent
    {
        public bool Ok;
        public bool Cancel;
        public Dir Dir;
    }

    private static int _frame = -1;
    private static Intent _intent;

    private static SelectorAccess _active;
    private static IntPtr _activeModelPtr;
    private static bool _backFocused;
    private static float _nextScan;

    internal static bool InGameplay => NavBehaviour.Scene.StartsWith("Enso");
    internal static bool BackVisible => _active != null;
    internal static bool BackFocused => _active != null && _backFocused;

    public static void OnSceneChanged() => SetActive(null);

    public static Intent Current
    {
        get
        {
            var f = Time.frameCount;
            if (f != _frame)
            {
                _frame = f;
                _intent = default;
                try { _intent = Compute(); }
                catch (Exception e) { Plugin.L.LogError($"[nav] {e}"); }
            }
            return _intent;
        }
    }

    private static Intent Compute()
    {
        var result = new Intent();
        if (InGameplay) { SetActive(null); return result; }

        var kb = Keyboard.current;
        if (kb == null) return result;

        var prev = kb[Key.D].wasPressedThisFrame;
        var next = kb[Key.K].wasPressedThisFrame;
        var ok = kb[Key.F].wasPressedThisFrame || kb[Key.J].wasPressedThisFrame;

        var any = prev || next || ok;
        if (any || Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + 0.25f;
            Scan(any);
        }

        if (prev ^ next)
        {
            var delta = next ? +1 : -1;
            if (_active != null) MoveSelector(delta);
            else result.Dir = delta > 0 ? Dir.Down : Dir.Up; // unknown screen: plain arrows
        }

        if (ok)
        {
            if (BackFocused)
            {
                _backFocused = false;
                result.Cancel = true;
                Plugin.L.LogInfo($"[nav] f{_frame} Back");
            }
            else
            {
                result.Ok = true;
            }
        }

        if (result.Ok || result.Cancel || result.Dir != Dir.None)
            Plugin.L.LogInfo($"[intent] f{_frame} ok={result.Ok} cancel={result.Cancel} dir={result.Dir}");
        return result;
    }

    private static void SetActive(SelectorAccess sel)
    {
        var ptr = sel?.Model.Pointer ?? IntPtr.Zero;
        if (ptr == _activeModelPtr) { _active = sel; return; }
        _active = sel;
        _activeModelPtr = ptr;
        _backFocused = false;
        if (sel != null)
            Plugin.L.LogInfo($"[nav] active selector {sel.Name} ({sel.View.name}) items={sel.Count} cur={sel.CurrentIndex}");
        else
            Plugin.L.LogInfo("[nav] no active selector");
    }

    private static void Scan(bool verbose)
    {
        var sw = Stopwatch.StartNew();
        SelectorAccess pick = null;
        var all = SelectorAccess.FindAll();
        foreach (var s in all)
        {
            var focused = s.IsFocused;
            var opened = s.IsOpened;
            if (verbose && Plugin.Probe.Value)
                Plugin.L.LogInfo($"[sel] {s.Name} view={s.View.name} focus={focused} opened={opened} n={s.Count}");
            if (focused && opened) pick = s;
        }
        if (verbose && Plugin.Probe.Value)
            Plugin.L.LogInfo($"[sel] scan {all.Count} selectors in {sw.Elapsed.TotalMilliseconds:F1} ms");
        SetActive(pick);
    }

    private static void MoveSelector(int delta)
    {
        var n = _active.Count;
        if (n <= 0) return;
        var slots = n + 1; // last slot = Back
        var cur = _backFocused ? n : _active.CurrentIndex;
        var target = ((cur + delta) % slots + slots) % slots;

        if (target == n)
        {
            _backFocused = true;
        }
        else
        {
            _backFocused = false;
            _active.Select(target);
        }
        Plugin.L.LogInfo($"[nav] {_active.Name}: {cur} -> {(target == n ? "Back" : target.ToString())}");
    }
}
