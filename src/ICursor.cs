using Il2CppInterop.Runtime;
using Scripts.Common.ControllerTypeSelector;
using UnityEngine;
using Dir = ControllerManager.Dir;

namespace DfjkNav;

/// <summary>Something on screen with a cursor over a list of items that native directions move.</summary>
internal interface ICursor
{
    string Name { get; }
    /// <summary>Identity, to notice when a different menu takes over.</summary>
    System.IntPtr Key { get; }
    int Count { get; }
    int CurrentIndex { get; }
    bool IsShown(int i);
    bool IsBusy { get; }

    /// <summary>Relative move for a native direction (+1, -1, 0); null when unknown.</summary>
    int? DirectionDelta(Dir dir);
    /// <summary>(columns, rows) for a grid walked with native steps; zero when not a grid.</summary>
    Vector2 GridSize { get; }
    /// <summary>Jump straight to an item; only for cursors whose view follows the model.</summary>
    bool CanSelectDirect { get; }
    void Select(int index);
    string Describe();

    /// <summary>Items change a value with left/right (sliders, option pickers): F/J toggles edit mode.</summary>
    bool Editable { get; }
}

/// <summary>
/// The "Controller Settings" popup (controller/keyboard vs drum) shown before song select.
/// It is a plain MonoBehaviour; left/right flips each player's button between the two types.
/// </summary>
internal sealed class ControllerTypeCursor : ICursor
{
    private readonly UiControllerTypeSelector _sel;
    private readonly UiControllerTypeButton _btn;

    private ControllerTypeCursor(UiControllerTypeSelector sel, UiControllerTypeButton btn)
    {
        _sel = sel;
        _btn = btn;
    }

    public static ControllerTypeCursor Find()
    {
        foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<UiControllerTypeSelector>()))
        {
            var sel = o.TryCast<UiControllerTypeSelector>();
            if (sel == null || !sel.isActiveAndEnabled || !sel.IsOpen) continue;
            var buttons = sel.buttons;
            if (buttons == null || buttons.Count == 0) continue;
            return new ControllerTypeCursor(sel, buttons[0]);
        }
        return null;
    }

    public string Name => "ControllerType";
    public System.IntPtr Key => _sel.Pointer;
    public int Count => 2;
    public int CurrentIndex => _btn.isTatacon ? 1 : 0;
    public bool IsShown(int i) => true;
    public bool IsBusy => false;

    public int? DirectionDelta(Dir dir) => dir switch
    {
        Dir.Right => CurrentIndex == 0 ? +1 : 0,
        Dir.Left => CurrentIndex == 1 ? -1 : 0,
        _ => 0,
    };

    public Vector2 GridSize => Vector2.zero;
    public bool CanSelectDirect => false;
    public void Select(int index) { }
    public string Describe() => $"ControllerType tatacon={_btn.isTatacon}";
    public bool Editable => false;
}
