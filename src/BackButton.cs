using UnityEngine;
using UnityEngine.UI;
using Il2CppInterop.Runtime;

namespace DfjkNav;

/// <summary>The injected on-screen "Back" slot. Pure uGUI built at runtime; no game assets.</summary>
internal static class BackButton
{
    private static GameObject _root;
    private static Image _bg;
    private static Text _label;
    private static RectTransform _rect;
    private static bool _visible, _focused;

    private static readonly Color BgIdle = new(0f, 0f, 0f, 0.55f);
    private static readonly Color BgFocus = new(1f, 0.82f, 0.1f, 1f);

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

        var btn = new GameObject("Back");
        btn.transform.SetParent(_root.transform, false);
        _rect = btn.AddComponent<RectTransform>();
        _rect.anchorMin = _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.anchoredPosition = new Vector2(24f, -96f);
        _rect.sizeDelta = new Vector2(220f, 64f);
        _bg = btn.AddComponent<Image>();
        _bg.raycastTarget = false;

        var txt = new GameObject("Label");
        txt.transform.SetParent(btn.transform, false);
        var tr = txt.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        _label = txt.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource(Il2CppType.Of<Font>(), "LegacyRuntime.ttf").Cast<Font>();
        _label.fontSize = 34;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.text = "◀  Back";
        _label.raycastTarget = false;

        Apply();
    }

    private static void Apply()
    {
        if (_root == null) return;
        _rect.gameObject.SetActive(_visible);
        _bg.color = _focused ? BgFocus : BgIdle;
        _label.color = _focused ? Color.black : Color.white;
        _rect.localScale = _focused ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
    }

    public static void Set(bool visible, bool focused)
    {
        if (_root == null)
        {
            try { Build(); }
            catch (System.Exception e) { Plugin.L.LogError($"[back] build failed: {e}"); return; }
        }
        if (visible == _visible && focused == _focused) return;
        _visible = visible;
        _focused = focused;
        Apply();
    }
}
