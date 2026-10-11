using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace DfjkNav;

/// <summary>
/// The injected on-screen slots (Back + extra actions), drawn as a row of chips on a dark strip over
/// the left of the game's bottom input guide. The guide's keyboard hints don't help a D/F/J/K player,
/// and the chips carry the same captions. Pure uGUI built at runtime; no game assets.
/// </summary>
internal static class Overlay
{
    private const float RowLeft = 18f, RowBottom = 12f, ChipHeight = 48f, Gap = 10f, PadX = 22f, StripPad = 10f;

    private static readonly Color StripColor = new(0.08f, 0.08f, 0.1f, 0.88f);
    private static readonly Color BgIdle = new(1f, 1f, 1f, 0.12f);
    private static readonly Color BgFocus = new(1f, 0.82f, 0.1f, 1f);
    private static readonly Color BgEdit = new(0.35f, 0.85f, 1f, 1f);
    private static readonly Color TextIdle = new(1f, 1f, 1f, 0.92f);

    private sealed class Chip
    {
        public RectTransform Rect;
        public Image Bg;
        public Text Label;
    }

    private static GameObject _root;
    private static RectTransform _strip;
    private static Font _font;
    private static readonly List<Chip> Chips = new();
    private static string _shownKey = "";

    private static void Build()
    {
        _root = new GameObject("DfjkNav.Canvas");
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        _font = Resources.GetBuiltinResource(Il2CppType.Of<Font>(), "LegacyRuntime.ttf").Cast<Font>();

        var strip = new GameObject("Strip");
        strip.transform.SetParent(_root.transform, false);
        _strip = strip.AddComponent<RectTransform>();
        _strip.anchorMin = _strip.anchorMax = Vector2.zero;
        _strip.pivot = Vector2.zero;
        var img = strip.AddComponent<Image>();
        img.color = StripColor;
        img.raycastTarget = false;
    }

    private static Chip NewChip()
    {
        var go = new GameObject("Chip");
        go.transform.SetParent(_root.transform, false);
        var c = new Chip { Rect = go.AddComponent<RectTransform>(), Bg = go.AddComponent<Image>() };
        c.Rect.anchorMin = c.Rect.anchorMax = Vector2.zero;
        c.Rect.pivot = new Vector2(0.5f, 0.5f);
        c.Bg.raycastTarget = false;

        var txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        var tr = txt.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        c.Label = txt.AddComponent<Text>();
        c.Label.font = _font;
        c.Label.fontSize = 27;
        c.Label.fontStyle = FontStyle.Bold;
        c.Label.alignment = TextAnchor.MiddleCenter;
        c.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        c.Label.raycastTarget = false;
        return c;
    }

    /// <param name="labels">Chip captions in ring order; null or empty hides the row.</param>
    /// <param name="focused">Index of the focused chip, or -1.</param>
    /// <param name="editing">Edit-mode hint instead of slots.</param>
    public static void Show(IReadOnlyList<string> labels, int focused, bool editing = false)
    {
        if (_root == null)
        {
            try { Build(); }
            catch (System.Exception e) { Plugin.L.LogError($"[overlay] build failed: {e}"); return; }
        }

        var key = labels == null ? "" : string.Join("|", labels) + "#" + focused + (editing ? "e" : "");
        if (key == _shownKey) return;
        _shownKey = key;

        var n = labels?.Count ?? 0;
        while (Chips.Count < n) Chips.Add(NewChip());
        _strip.gameObject.SetActive(n > 0);

        var x = RowLeft;
        for (var i = 0; i < Chips.Count; i++)
        {
            var c = Chips[i];
            var on = i < n;
            c.Rect.gameObject.SetActive(on);
            if (!on) continue;

            var isFocus = i == focused;
            c.Label.text = i == 0 && !editing ? "◀ " + labels[i] : labels[i];
            var w = Mathf.Max(110f, c.Label.preferredWidth + PadX * 2);
            c.Rect.sizeDelta = new Vector2(w, ChipHeight);
            c.Rect.anchoredPosition = new Vector2(x + w / 2f, RowBottom + ChipHeight / 2f);
            c.Rect.localScale = isFocus && !editing ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one;
            c.Bg.color = isFocus ? (editing ? BgEdit : BgFocus) : BgIdle;
            c.Label.color = isFocus ? Color.black : TextIdle;
            x += w + Gap;
        }

        _strip.anchoredPosition = new Vector2(0f, RowBottom - StripPad);
        _strip.sizeDelta = new Vector2(x - Gap + RowLeft, ChipHeight + StripPad * 2);
    }
}
