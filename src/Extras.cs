using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Scripts.Common.InputGuide;
using TMPro;
using UnityEngine;

namespace DfjkNav;

/// <summary>What an extra slot does when confirmed with F/J.</summary>
internal enum ExtraKind { Cancel, X, Y, Menu, TabPrev, TabNext }

internal readonly struct Extra
{
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
    public static List<Extra> Build()
    {
        var list = new List<Extra>();
        string back = null;
        var guide = FindVisibleGuide();
        if (guide != null)
        {
            if (guide.IsActiveGroupB()) back = Label(guide.groupB);
            if (guide.IsActiveGroupX()) Add(list, ExtraKind.X, Label(guide.groupX));
            if (guide.IsActiveGroupY()) Add(list, ExtraKind.Y, Label(guide.groupY));
            if (guide.IsActiveGroupMenu()) Add(list, ExtraKind.Menu, Label(guide.groupMenu));
        }
        list.Insert(0, new Extra(ExtraKind.Cancel, string.IsNullOrWhiteSpace(back) ? "Back" : back));

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
