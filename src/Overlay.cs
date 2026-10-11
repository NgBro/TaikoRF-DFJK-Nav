using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace DfjkNav;

/// <summary>
/// The injected on-screen slots (Back + extra actions), drawn as a row of chips just above the
/// game's own bottom input guide. Pure uGUI built at runtime; no game assets.
/// </summary>
internal static class Overlay
{
    private const float RowLeft = 28f, RowBottom = 100f, ChipHeight = 54f, Gap = 12f, PadX = 26f;

    private static readonly Color BgIdle = new(0f, 0f, 0f, 0.55f);
    private static readonly Color BgFocus = new(1f, 0.82f, 0.1f, 1f);
    private static readonly Color TextIdle = new(1f, 1f, 1f, 0.9f);

    private sealed class Chip
    {
        public RectTransform Rect;
        public Image Bg;
        public Text Label;
    }

    private static GameObject _root;
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
    }

    private static Chip NewChip()
    {
        var go = new GameObject("Chip");
        go.transform.SetParent(_root.transform, false);
        var c = new Chip { Rect = go.AddComponent<RectTransform>(), Bg = go.AddComponent<Image>() };
        c.Rect.anchorMin = c.Rect.anchorMax = Vector2.zero;
        c.Rect.pivot = new Vector2(0.5f, 0f);
        c.Bg.raycastTarget = false;

        var txt = new GameObject("Label");
        txt.transform.SetParent(go.transform, false);
        var tr = txt.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        c.Label = txt.AddComponent<Text>();
        c.Label.font = _font;
        c.Label.fontSize = 30;
        c.Label.fontStyle = FontStyle.Bold;
        c.Label.alignment = TextAnchor.MiddleCenter;
        c.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        c.Label.raycastTarget = false;
        return c;
    }

    /// <param name="labels">Chip captions in ring order; null or empty hides the row.</param>
    /// <param name="focused">Index of the focused chip, or -1.</param>
    public static void Show(IReadOnlyList<string> labels, int focused)
    {
        if (_root == null)
        {
            try { Build(); }
            catch (System.Exception e) { Plugin.L.LogError($"[overlay] build failed: {e}"); return; }
        }

        var key = labels == null ? "" : string.Join("|", labels) + "#" + focused;
        if (key == _shownKey) return;
        _shownKey = key;

        var n = labels?.Count ?? 0;
        while (Chips.Count < n) Chips.Add(NewChip());

        var x = RowLeft;
        for (var i = 0; i < Chips.Count; i++)
        {
            var c = Chips[i];
            var on = i < n;
            c.Rect.gameObject.SetActive(on);
            if (!on) continue;

            var isFocus = i == focused;
            c.Label.text = i == 0 ? "◀  " + labels[i] : labels[i];
            var w = Mathf.Max(150f, c.Label.preferredWidth + PadX * 2);
            c.Rect.sizeDelta = new Vector2(w, ChipHeight);
            c.Rect.anchoredPosition = new Vector2(x + w / 2f, RowBottom);
            c.Rect.localScale = isFocus ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
            c.Bg.color = isFocus ? BgFocus : BgIdle;
            c.Label.color = isFocus ? Color.black : TextIdle;
            x += w + Gap;
        }
    }
}
