using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Scripts.Common.InputGuide;
using TMPro;
using UnityEngine;

namespace DfjkNav;

/// <summary>What an extra slot does when confirmed with F/J.</summary>
internal enum ExtraKind { Cancel, Ok, X, Y, Menu, TabPrev, TabNext, Left, Right, Up, Down }

internal readonly struct Extra
{
    public bool IsDirection => Kind is ExtraKind.Left or ExtraKind.Right or ExtraKind.Up or ExtraKind.Down;

    public readonly ExtraKind Kind;
    public readonly string Label;
    public Extra(ExtraKind kind, string label) { Kind = kind; Label = label; }
}

/// <summary>
/// Builds the extra ring slots for the current screen: Back first, then whatever actions the game's own
/// bottom input guide shows (Playlist, Help, ...), then tab switching if the screen polls LB/RB.
/// </summary>
internal static class Extras
{
    /// <param name="hasCursor">
    /// A known cursor covers the screen's items. Without one, screens that poll directions (e.g. the
    /// rewards map) get arrow slots so their content can still be scrolled.
    /// </param>
    /// <param name="editable">F/J on items toggles edit mode, so the page's own confirm gets a slot.</param>
    public static List<Extra> Build(bool hasCursor, bool editable)
    {
        var list = new List<Extra>();
        string back = null;
        var guide = FindVisibleGuide();
        if (editable)
        {
            var ok = guide != null && guide.IsActiveGroupA() ? Label(guide.groupA) : null;
            list.Add(new Extra(ExtraKind.Ok, string.IsNullOrWhiteSpace(ok) ? "Confirm" : ok));
        }
        if (guide != null)
        {
            if (guide.IsActiveGroupB()) back = Label(guide.groupB);
            if (guide.IsActiveGroupX()) Add(list, ExtraKind.X, Label(guide.groupX));
            if (guide.IsActiveGroupY()) Add(list, ExtraKind.Y, Label(guide.groupY));
            if (guide.IsActiveGroupMenu()) Add(list, ExtraKind.Menu, Label(guide.groupMenu));
        }
        list.Insert(0, new Extra(ExtraKind.Cancel, string.IsNullOrWhiteSpace(back) ? "Back" : back));

        if (!hasCursor)
        {
            if (QueryTracker.RecentlyAsked(QueryTracker.Horizontal))
            {
                list.Add(new Extra(ExtraKind.Left, "◀"));
                list.Add(new Extra(ExtraKind.Right, "▶"));
            }
            if (QueryTracker.RecentlyAsked(QueryTracker.Vertical))
            {
                list.Add(new Extra(ExtraKind.Up, "▲"));
                list.Add(new Extra(ExtraKind.Down, "▼"));
            }
        }

        if (QueryTracker.RecentlyAsked(QueryTracker.Tab))
        {
            list.Add(new Extra(ExtraKind.TabPrev, "◀ Tab"));
            list.Add(new Extra(ExtraKind.TabNext, "Tab ▶"));
        }
        return list;
    }

    private static void Add(List<Extra> list, ExtraKind kind, string label)
    {
        if (!string.IsNullOrWhiteSpace(label)) list.Add(new Extra(kind, label));
    }

    private static UiInputGuide FindVisibleGuide()
    {
        UiInputGuide best = null;
        foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<UiInputGuide>()))
        {
            var g = o.TryCast<UiInputGuide>();
            if (g == null || !g.isActiveAndEnabled) continue;
            var cg = g.CanvasGroup;
            if (cg != null && cg.alpha < 0.5f) continue;
            best = g; // later (deeper in hierarchy / newer windows) wins
        }
        return best;
    }

    /// <summary>The group's caption (skips the TMP that only renders the key/button sprite).</summary>
    private static string Label(UiGroupText group)
    {
        if (group == null) return null;
        foreach (var t in group.GetComponentsInChildren<TextMeshProUGUI>())
        {
            var s = t.text;
            if (string.IsNullOrWhiteSpace(s) || s.Contains("<sprite")) continue;
            return s.Trim();
        }
        return null;
    }
}
