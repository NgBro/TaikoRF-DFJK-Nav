using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Dir = ControllerManager.Dir;

namespace DfjkNav;

/// <summary>
/// Turns D/F/J/K into menu intents, once per frame, the first time anyone asks
/// (our Update or one of the game's ControllerManager queries), so the order of
/// MonoBehaviour updates doesn't matter.
///
/// The ring on every menu screen is: [the active selector's items] + [extras], where extras are
/// Back plus the actions on the game's own input guide. Without a known selector the ring is
/// [the screen itself] + [extras], and F/J on the screen slot is a plain confirm.
/// </summary>
internal static class Nav
{
    internal struct Intent
    {
        public bool Ok, Cancel, X, Y, Menu, TabPrev, TabNext;
        public Dir Dir;

        /// <summary>A direction reported as held this frame (D/K held on a scrolling list).</summary>
        public Dir HeldDir;

        /// <summary>
        /// D/F/J/K is physically down in a menu. The game has its own meanings for some of these keys
        /// (D is "Right" in song select), so menu queries must report only our intent this frame.
        /// </summary>
        public bool Suppress;

        public bool Any => Ok || Cancel || X || Y || Menu || TabPrev || TabNext || Dir != Dir.None;
    }

    // Each injected step waits until the game's cursor index actually changes (scrollers ignore input
    // while their scroll animation runs), then a short gap. A step that is never acknowledged is retried.
    private const int StepGap = 2;
    private const float AckTimeout = 0.45f;
    private const int MaxRetries = 2;
    private const float RepeatDelay = 0.35f, RepeatInterval = 0.06f, ExtraRepeatInterval = 0.3f;

    private static int _frame = -1;
    private static Intent _intent;

    private static ICursor _active;
    private static IntPtr _activeModelPtr;
    private static List<Extra> _extras = new() { new Extra(ExtraKind.Cancel, "Back") };
    private static int _extraFocus = -1; // -1 = on the game's items / screen
    private static bool _editing;        // D/K adjust the focused item's value
    private static Dir _editDir;
    private static float _nextScan;

    // Native direction steps still to feed the game, and the index they should end on.
    private static readonly List<Dir> Steps = new();
    private static int _nextStepFrame;
    private static int _expectIndex = -1;
    private static bool _okAfterSteps;
    private static bool _awaitingAck;
    private static int _ackFrom;
    private static float _ackDeadline;
    private static Dir _inFlight;
    private static int _retries;

    private static bool Busy => Steps.Count > 0 || _awaitingAck;
    private static Dir _dirNext = Dir.None, _dirPrev = Dir.None;

    // Hold-to-repeat for D/K. On scrollers a hold uses the game's fast-repeat scrolling.
    private static float _holdSince, _nextRepeat;
    private static bool _fastRepeat;

    /// <summary>A held D/K is scrolling a list: report it to the game as a fast repeat.</summary>
    internal static bool FastRepeat => _fastRepeat && !InGameplay;

    internal static bool InGameplay => EnsoGate.Playing;

    internal static bool OverlayVisible => !InGameplay;
    internal static IReadOnlyList<Extra> CurrentExtras => _extras;
    internal static int ExtraFocus => InGameplay ? -1 : _extraFocus;
    internal static bool Editing => !InGameplay && _editing && _active != null;

    public static void OnSceneChanged()
    {
        SetActive(null);
        _extraFocus = -1;
        _editing = false;
    }

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

        result.Suppress = Touched(kb, Plugin.PrevKey) || Touched(kb, Plugin.NextKey)
                          || Touched(kb, Plugin.ConfirmKey) || Touched(kb, Plugin.BackKey);

        var prev = kb[Plugin.PrevKey].wasPressedThisFrame;
        var next = kb[Plugin.NextKey].wasPressedThisFrame;
        var ok = kb[Plugin.ConfirmKey].wasPressedThisFrame;
        var back = Plugin.BackKey != Key.None && kb[Plugin.BackKey].wasPressedThisFrame && !ok;

        // Hold D or K to keep moving.
        var now = Time.unscaledTime;
        var repeat = false;
        var holding = kb[Plugin.PrevKey].isPressed ^ kb[Plugin.NextKey].isPressed;
        var holdDelta = kb[Plugin.PrevKey].isPressed ? -1 : +1;
        if (prev || next) { _holdSince = now; _nextRepeat = now + RepeatDelay; }
        _fastRepeat = false;

        if (holding && !prev && !next && now - _holdSince >= RepeatDelay)
        {
            if (HoldScroll(holdDelta, ref result)) { /* the game scrolls on its own while "held" */ }
            else if (now >= _nextRepeat && !Busy) // repeat no faster than the game accepts steps
            {
                // Slow down on the extra slots so a held key can stop on one.
                _nextRepeat = now + (_extraFocus >= 0 || _active == null ? ExtraRepeatInterval : RepeatInterval);
                prev = holdDelta < 0;
                next = !prev;
                repeat = true;
            }
        }

        var any = (prev || next || ok || back) && !repeat;
        if (any || (now >= _nextScan && !Busy && _expectIndex < 0))
        {
            _nextScan = now + 0.25f;
            Scan(any && Plugin.Probe.Value);
        }

        if (prev ^ next)
        {
            if (_editing && _active != null) _editDir = next ? Dir.Right : Dir.Left;
            else Move(next ? +1 : -1);
        }
        if (_editDir != Dir.None) { result.Dir = _editDir; _editDir = Dir.None; }

        if (back)
        {
            if (_editing)
            {
                _editing = false; // leave the value as it is; back out of the page with a second press
                Plugin.Dbg($"[nav] f{_frame} edit done (back key)");
            }
            else
            {
                _extraFocus = -1;
                ClearSteps();
                result.Cancel = true;
                Plugin.Dbg($"[nav] f{_frame} back key");
            }
        }

        if (ok)
        {
            if (_editing)
            {
                _editing = false;
                Plugin.Dbg($"[nav] f{_frame} edit done");
            }
            else if (_extraFocus < 0 && _active != null && _active.Editable && !Busy && _expectIndex < 0)
            {
                _editing = true;
                Plugin.Dbg($"[nav] f{_frame} editing item {_active.CurrentIndex}");
            }
            else if (_extraFocus >= 0 && _extraFocus < _extras.Count)
            {
                var e = _extras[_extraFocus];
                if (!e.IsDirection) _extraFocus = -1; // arrows stay focused so confirm can be tapped repeatedly
                ClearSteps();
                Fire(e.Kind, ref result);
                Plugin.Dbg($"[nav] f{_frame} extra {e.Kind} \"{e.Label}\"");
            }
            else if (Busy || _expectIndex >= 0)
            {
                _okAfterSteps = true; // confirm once the cursor has arrived
            }
            else
            {
                result.Ok = true;
            }
        }

        PumpSteps(ref result);

        if (result.HeldDir != Dir.None && _frame % 30 == 0)
            Plugin.Dbg($"[intent] f{_frame} hold {result.HeldDir} at {_active?.CurrentIndex}");
        if (result.Any)
            Plugin.Dbg($"[intent] f{_frame} ok={result.Ok} cancel={result.Cancel} dir={result.Dir}" +
                             (result.X || result.Y || result.Menu || result.TabPrev || result.TabNext
                                 ? $" x={result.X} y={result.Y} menu={result.Menu} tab={result.TabPrev}/{result.TabNext}" : ""));
        return result;
    }

    /// <summary>
    /// D/K held on a scroller (song list): report the direction as held so the game runs its own
    /// continuous fast scroll, until the cursor reaches the list edge where the extra slots begin.
    /// Returns false when the normal one-step repeat should run instead.
    /// </summary>
    private static bool HoldScroll(int delta, ref Intent result)
    {
        if (_active == null || _extraFocus >= 0 || Busy || _expectIndex >= 0) return false;
        if (_active.DirectionDelta(Dir.Down) == null) return false;

        var d = delta > 0 ? _dirNext : _dirPrev;
        if (d == Dir.None)
        {
            foreach (var cand in new[] { Dir.Down, Dir.Up, Dir.Right, Dir.Left })
                if (_active.DirectionDelta(cand) == delta) { d = cand; break; }
            if (d == Dir.None) return false;
            if (delta > 0) _dirNext = d; else _dirPrev = d;
        }

        var n = _active.Count;
        var idx = _active.CurrentIndex;
        var atEdge = delta > 0 ? idx >= n - 1 : idx <= 0;
        if (atEdge)
        {
            // Stop here; once the last scroll settles, the one-step repeat moves into the extras.
            return _active.IsBusy;
        }

        result.HeldDir = d;
        _fastRepeat = true;
        return true;
    }

    private static void Fire(ExtraKind kind, ref Intent r)
    {
        switch (kind)
        {
            case ExtraKind.Cancel: r.Cancel = true; break;
            case ExtraKind.Ok: r.Ok = true; break;
            case ExtraKind.X: r.X = true; break;
            case ExtraKind.Y: r.Y = true; break;
            case ExtraKind.Menu: r.Menu = true; break;
            case ExtraKind.TabPrev: r.TabPrev = true; break;
            case ExtraKind.TabNext: r.TabNext = true; break;
            case ExtraKind.Left: r.Dir = Dir.Left; break;
            case ExtraKind.Right: r.Dir = Dir.Right; break;
            case ExtraKind.Up: r.Dir = Dir.Up; break;
            case ExtraKind.Down: r.Dir = Dir.Down; break;
            case ExtraKind.Genre: SongSelectHooks.FocusGenres(); break;
        }
    }

    // ---- ring movement ---------------------------------------------------------------------

    private static void Move(int delta)
    {
        var e = _extras.Count;

        if (_active == null)
        {
            // Ring = [screen] + extras.
            var slots = 1 + e;
            var from = _extraFocus + 1;
            var to = ((from + delta) % slots + slots) % slots;
            _extraFocus = to - 1;
            Plugin.Dbg($"[nav] screen ring: {from} -> {to} ({(to == 0 ? "screen" : _extras[to - 1].Label)})");
            return;
        }

        var n = _active.Count;
        if (n <= 0) { _extraFocus = _extraFocus < 0 ? 0 : -1; return; }

        var total = n + e;
        // Where the game's cursor is (or will be once queued steps land).
        var cursor = _expectIndex >= 0 ? _expectIndex : _active.CurrentIndex;
        var start = _extraFocus >= 0 ? n + _extraFocus : cursor;
        var target = start;
        for (var step = 0; step < total; step++)
        {
            target = ((target + delta) % total + total) % total;
            if (target >= n || _active.IsShown(target)) break;
        }

        Plugin.Dbg($"[nav] {_active.Name}: {Slot(start, n)} -> {Slot(target, n)}");

        if (target >= n)
        {
            _extraFocus = target - n; // the game's cursor stays where it is
            return;
        }

        _extraFocus = -1;
        if (target == cursor) return;
        MoveCursor(cursor, target, delta);
    }

    private static string Slot(int s, int n) => s < n ? s.ToString() : _extras[s - n].Label;

    private static void MoveCursor(int cursor, int target, int delta)
    {
        if (_active.DirectionDelta(Dir.Down) != null)
        {
            var d = delta > 0 ? _dirNext : _dirPrev;
            if (d == Dir.None && !Busy && _expectIndex < 0)
            {
                var probe = "";
                foreach (var cand in new[] { Dir.Down, Dir.Up, Dir.Right, Dir.Left })
                {
                    // Scrollers return a relative move (+1/-1/0); grid selectors may return the new index.
                    var r = _active.DirectionDelta(cand) ?? 0;
                    probe += $" {cand}={r}";
                    if (d == Dir.None && r != 0 && r == delta) d = cand;
                }
                Plugin.Dbg($"[nav] {_active.Name}: DirectionToMove from {cursor}:{probe}");
                if (d != Dir.None)
                {
                    if (delta > 0) _dirNext = d; else _dirPrev = d;
                    Plugin.Dbg($"[nav] {_active.Name}: {(delta > 0 ? "next" : "prev")} = {d}");
                }
            }
            if (d != Dir.None) { Enqueue(d, target); return; }
        }
        else if (PlanGridSteps(cursor, target))
        {
            return;
        }

        // Free-form menu (no grid, no DirectionToMove): the model's Select drives the view directly.
        if (_active.CanSelectDirect) _active.Select(target);
        else Plugin.L.LogWarning($"[nav] {_active.Name}: no way to move {cursor} -> {target}");
    }

    private static void Enqueue(Dir d, int target)
    {
        Steps.Add(d);
        _expectIndex = target;
        if (_nextStepFrame < _frame) _nextStepFrame = _frame;
    }

    /// <summary>Queue native direction presses that walk a MenuSelector grid cursor between indices.</summary>
    private static bool PlanGridSteps(int from, int to)
    {
        var grid = _active.GridSize;
        var cols = Mathf.RoundToInt(grid.x);
        var rows = Mathf.RoundToInt(grid.y);
        if (cols <= 0 || rows <= 0) return false;

        var dx = to % cols - from % cols;
        var dy = to / cols - from / cols;
        if (dx == 0 && dy == 0) return false;
        for (var i = 0; i < Math.Abs(dy); i++) Enqueue(dy > 0 ? Dir.Down : Dir.Up, to);
        for (var i = 0; i < Math.Abs(dx); i++) Enqueue(dx > 0 ? Dir.Right : Dir.Left, to);
        return true;
    }

    private static bool Touched(Keyboard kb, Key k) =>
        k != Key.None && (kb[k].isPressed || kb[k].wasReleasedThisFrame);

    private static void PumpSteps(ref Intent result)
    {
        var now = Time.unscaledTime;

        if (_awaitingAck)
        {
            var idx = _active?.CurrentIndex ?? _ackFrom;
            if (idx == _ackFrom && now < _ackDeadline) return;
            // Taken: let the scroll animation finish, or the next step is dropped. (A held D/K reports
            // fast repeat, which makes the game use its shorter fast-scroll animation.)
            if (idx != _ackFrom && _active != null && _active.IsBusy && now < _ackDeadline + AckTimeout) return;
            _awaitingAck = false;
            if (idx == _ackFrom && _active != null && _active.CanSelectDirect)
            {
                // Some menus only switch with other buttons (song select's store tabs use LB/RB);
                // their model renders a direct select, so don't wait for retries.
                Plugin.Dbg($"[nav] step {_inFlight} not taken at {idx}; selecting {_expectIndex} directly");
                Steps.Clear();
                _active.Select(_expectIndex);
            }
            else if (idx == _ackFrom && _active != null)
            {
                if (_retries++ < MaxRetries)
                {
                    Plugin.Dbg($"[nav] step {_inFlight} not taken at {idx}; retrying");
                    Steps.Insert(0, _inFlight);
                }
                else Plugin.L.LogWarning($"[nav] step {_inFlight} not taken at {idx}; giving up");
            }
            else _retries = 0;
        }

        if (Steps.Count > 0)
        {
            if (_frame < _nextStepFrame || result.Any) return;
            _inFlight = Steps[0];
            Steps.RemoveAt(0);
            result.Dir = _inFlight;
            _ackFrom = _active?.CurrentIndex ?? -1;
            _awaitingAck = _active != null;
            _ackDeadline = now + AckTimeout;
            _nextStepFrame = _frame + StepGap;
            return;
        }

        if (_expectIndex < 0) return;

        if (_active != null)
        {
            var at = _active.CurrentIndex;
            if (at != _expectIndex)
            {
                Plugin.L.LogWarning($"[nav] {_active.Name}: steps ended on {at}, expected {_expectIndex}");
                if (_active.CanSelectDirect) _active.Select(_expectIndex);
            }
        }
        _expectIndex = -1;
        _retries = 0;

        if (_okAfterSteps)
        {
            _okAfterSteps = false;
            result.Ok = true;
        }
    }

    private static void ClearSteps()
    {
        Steps.Clear();
        _expectIndex = -1;
        _okAfterSteps = false;
        _awaitingAck = false;
        _retries = 0;
    }

    // ---- discovery -------------------------------------------------------------------------

    private static void SetActive(ICursor sel)
    {
        var ptr = sel?.Key ?? IntPtr.Zero;
        if (ptr == _activeModelPtr) { _active = sel; return; }
        _active = sel;
        _activeModelPtr = ptr;
        _extraFocus = -1;
        _editing = false;
        _dirNext = _dirPrev = Dir.None;
        ClearSteps();
        if (sel != null)
            Plugin.Dbg($"[nav] active {sel.Describe()}");
        else
            Plugin.Dbg("[nav] no active selector");
    }

    private static void Scan(bool verbose)
    {
        var sw = Stopwatch.StartNew();
        SelectorAccess pick = null;
        var pickOpened = false;
        var pickCount = 0;
        var all = SelectorAccess.FindAll();
        foreach (var s in all)
        {
            var focused = s.IsFocused;
            var opened = s.IsOpened;
            if (verbose)
                Plugin.Dbg($"[sel] {s.Family} {s.Name} view={s.View.name} focus={focused} opened={opened} n={s.Count}");
            // Some selectors never flip Opened (Thunder Shrine's), so Focus decides. Ties: an opened one,
            // then the longer list (song select keeps its 3 store tabs focused next to the song list).
            if (!focused) continue;
            var count = s.Count;
            if (pick == null || (opened && !pickOpened) || (opened == pickOpened && count > pickCount))
            {
                pick = s;
                pickOpened = opened;
                pickCount = count;
            }
        }
        // Screen-specific cursors that aren't MenuSelectors take over while open.
        ICursor chosen = (ICursor)ControllerTypeCursor.Find() ?? pick;
        SetActive(chosen);

        var extras = Extras.Build(chosen);
        if (!SameExtras(extras))
        {
            _extras = extras;
            if (_extraFocus >= _extras.Count) _extraFocus = -1;
            var labels = new List<string>();
            foreach (var x in extras) labels.Add($"{x.Kind}:{x.Label}");
            Plugin.Dbg($"[nav] extras: {string.Join(", ", labels)}");
        }
        if (verbose)
            Plugin.Dbg($"[sel] scan {all.Count} selectors in {sw.Elapsed.TotalMilliseconds:F1} ms");
    }

    private static bool SameExtras(List<Extra> other)
    {
        if (other.Count != _extras.Count) return false;
        for (var i = 0; i < other.Count; i++)
            if (other[i].Kind != _extras[i].Kind || other[i].Label != _extras[i].Label) return false;
        return true;
    }
}
