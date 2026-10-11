using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using BF = Il2CppSystem.Reflection.BindingFlags;
using Dir = ControllerManager.Dir;
using Il2Obj = Il2CppSystem.Object;
using Il2Type = Il2CppSystem.Type;
using Il2Method = Il2CppSystem.Reflection.MethodInfo;
using Il2Prop = Il2CppSystem.Reflection.PropertyInfo;

namespace DfjkNav;

internal enum Family
{
    Menu,     // Scripts.Common.Selector.MenuSelector<T>            / UiMenuSelector<,,>
    Infinite, // Scripts.Common.Scroller.InfiniteScroller<T>        / UiInfiniteScroller<,,>  (song list, genres)
    Grid,     // ScrollableGridSelector<T>                          / UiScrollableGridSelector<,,> (shop)
}

/// <summary>
/// Reflection wrapper over the game's generic selector models, reached through their views
/// (<c>UiView&lt;TModel&gt;.Model</c>). Generic IL2CPP classes can't be named from C#, so
/// everything goes through il2cpp reflection.
/// </summary>
internal sealed class SelectorAccess : ICursor
{
    private const BF Inst = BF.Instance | BF.Public | BF.NonPublic;

    private static readonly Dictionary<string, Family> ViewBases = new()
    {
        ["UiMenuSelector`3"] = Family.Menu,
        ["UiInfiniteScroller`3"] = Family.Infinite,
        ["UiScrollableGridSelector`3"] = Family.Grid,
    };

    public readonly MonoBehaviour View;
    public readonly Il2Obj Model;
    public string Name { get; }
    public readonly Family Family;

    private readonly Il2Type _viewBase;
    private readonly Il2Method _getCurrentIndex;
    private readonly Il2Method _selectIndex;
    private readonly Il2Method _selectItem;
    private readonly Il2Method _directionToMove;
    private readonly Il2Prop _itemList;
    private readonly Il2Prop _focus;
    private readonly Il2Prop _opened;

    private SelectorAccess(MonoBehaviour view, Il2Type viewBase, Family family, Il2Obj model)
    {
        View = view;
        _viewBase = viewBase;
        Family = family;
        Model = model;
        var modelType = model.GetIl2CppType();
        Name = modelType.Name;
        _getCurrentIndex = modelType.GetMethod("GetCurrentIndex", Inst);
        _directionToMove = modelType.GetMethod("DirectionToMove", Inst);
        foreach (var m in modelType.GetMethods(Inst))
        {
            if (m.Name != "Select") continue;
            var ps = m.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.FullName == "System.Int32") _selectIndex = m;
            else if (ps.Length == 1) _selectItem = m;
        }
        _itemList = modelType.GetProperty("ItemList", Inst);
        _focus = modelType.GetProperty("Focus", Inst);
        _opened = modelType.GetProperty("Opened", Inst);
    }

    private bool Valid => _getCurrentIndex != null && _selectIndex != null && _itemList != null && _focus != null;

    // ---- discovery -------------------------------------------------------------------------

    private static readonly Dictionary<string, (Il2Type viewBase, Family family)?> ViewCache = new();

    private static (Il2Type viewBase, Family family)? Classify(Il2Type t)
    {
        var key = t.FullName;
        if (ViewCache.TryGetValue(key, out var hit)) return hit;
        (Il2Type, Family)? result = null;
        for (var bt = t; bt != null; bt = bt.BaseType)
            if (ViewBases.TryGetValue(bt.Name, out var fam)) { result = (bt, fam); break; }
        ViewCache[key] = result;
        return result;
    }

    private static Il2Obj GetModel(MonoBehaviour view, Il2Type t)
    {
        for (var bt = t; bt != null; bt = bt.BaseType)
        {
            if (bt.Name != "UiView`1") continue;
            return bt.GetProperty("Model", Inst)?.GetValue(view);
        }
        return null;
    }

    /// <summary>All selector views currently alive and enabled in the scene.</summary>
    public static List<SelectorAccess> FindAll()
    {
        var list = new List<SelectorAccess>();
        foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<MonoBehaviour>()))
        {
            var mb = o.TryCast<MonoBehaviour>();
            if (mb == null || !mb.isActiveAndEnabled) continue;
            var t = mb.GetIl2CppType();
            var cls = Classify(t);
            if (cls == null) continue;
            var model = GetModel(mb, t);
            if (model == null) continue;
            var acc = new SelectorAccess(mb, cls.Value.viewBase, cls.Value.family, model);
            if (acc.Valid) list.Add(acc);
            else Plugin.L.LogWarning($"[sel] {acc.Name}: missing members");
        }
        return list;
    }

    // ---- state -----------------------------------------------------------------------------

    private static Il2Obj ValueOf(Il2Obj reactive) =>
        reactive?.GetIl2CppType().GetProperty("Value", Inst)?.GetValue(reactive);

    private static bool ReadReactiveBool(Il2Prop prop, Il2Obj target)
    {
        var v = ValueOf(prop?.GetValue(target));
        return v != null && v.Unbox<bool>();
    }

    public bool IsFocused => ReadReactiveBool(_focus, Model);
    public bool IsOpened => _opened == null || ReadReactiveBool(_opened, Model);

    private Il2Obj ItemListObj() => ValueOf(_itemList.GetValue(Model));

    public int Count
    {
        get
        {
            var list = ItemListObj();
            var count = list?.GetIl2CppType().GetProperty("Count", Inst)?.GetValue(list);
            return count == null ? 0 : count.Unbox<int>();
        }
    }

    public Il2Obj Item(int i)
    {
        var list = ItemListObj();
        var getItem = list?.GetIl2CppType().GetMethod("get_Item", Inst);
        return getItem?.Invoke(list, Args(BoxInt(i)));
    }

    public int CurrentIndex => _getCurrentIndex.Invoke(Model, Args()).Unbox<int>();

    /// <summary>
    /// The view's button for item <paramref name="i"/> is shown. Menus keep hidden entries in
    /// ItemList (e.g. 3P/4P in the player-count picker), so this is what decides reachability.
    /// Scrollers recycle their buttons, so every item counts as shown there.
    /// </summary>
    public bool IsShown(int i)
    {
        if (Family != Family.Menu) return true;
        var buttons = _viewBase.GetField("Buttons", Inst)?.GetValue(View);
        if (buttons == null) return true;
        var t = buttons.GetIl2CppType();
        var cnt = t.GetProperty("Count", Inst)?.GetValue(buttons);
        if (cnt == null || i >= cnt.Unbox<int>()) return true;
        var btn = t.GetMethod("get_Item", Inst)?.Invoke(buttons, Args(BoxInt(i)));
        var comp = btn?.TryCast<Component>();
        return comp == null || comp.gameObject.activeInHierarchy;
    }

    /// <summary>(columns, rows) from MenuSelector.SetSize; (0,0) for free-form menus and other families.</summary>
    public Vector2 GridSize
    {
        get
        {
            var v = Model.GetIl2CppType().GetProperty("GridSize", Inst)?.GetValue(Model);
            return v == null ? Vector2.zero : v.Unbox<Vector2>();
        }
    }

    /// <summary>
    /// The selector is mid-animation and would drop input (song list: model IsAnimating / view IsScrolling).
    /// False for selectors that expose neither.
    /// </summary>
    public bool IsBusy
    {
        get
        {
            var anim = Model.GetIl2CppType().GetProperty("IsAnimating", Inst)?.GetValue(Model);
            if (anim != null && anim.Unbox<bool>()) return true;
            var scrolling = ValueOf(View.GetIl2CppType().GetProperty("IsScrolling", Inst)?.GetValue(View));
            return scrolling != null && scrolling.Unbox<bool>();
        }
    }

    /// <summary>Scroller/grid families: how far <paramref name="dir"/> moves the cursor (Down=+1, Right=+4 on songs).</summary>
    public int? DirectionDelta(Dir dir)
    {
        if (_directionToMove == null) return null;
        var r = _directionToMove.Invoke(Model, Args(BoxDir(dir)));
        return r?.Unbox<int>();
    }

    public System.IntPtr Key => Model.Pointer;

    // Selectors whose items are adjusted with left/right rather than picked.
    private static readonly HashSet<string> EditableNames = new()
    {
        "VolumeSelector", "NoteAdjustmentSelector", "GraphicSettingSelector", "OnlineOptionSelector",
        "DiffOption", "TwoPlayerOption", "TrainingOptionWindow", "WarNpcSetting", "TimingDelaySelector",
        "RoomCreateMenu", "RoomQuickCreateMenu", "RoomSearchFilterMenu", "OnlineRankCustomizeSelector",
    };

    private bool? _editable;

    public bool Editable => _editable ??= ComputeEditable();

    private bool ComputeEditable()
    {
        for (var t = Model.GetIl2CppType(); t != null; t = t.BaseType)
            if (EditableNames.Contains(t.Name) || t.Name == "ContentChangableSelector`2") return true;
        return false;
    }

    // Free-form MenuSelectors (main menu) render a direct Select; scrollers and grid cursors desync.
    public bool CanSelectDirect => Family == Family.Menu;

    public string Describe()
    {
        var shown = "";
        if (Family == Family.Menu)
            for (var i = 0; i < Count; i++) shown += IsShown(i) ? "1" : "0";
        return $"{Family} {Name} ({View.name}) items={Count} shown={shown} grid={GridSize} cur={CurrentIndex}" +
               (Editable ? " editable" : "");
    }

    public void Select(int index)
    {
        var item = Family == Family.Menu ? Item(index) : null;
        if (_selectItem != null && item != null) _selectItem.Invoke(Model, Args(item));
        else _selectIndex.Invoke(Model, Args(BoxInt(index)));
    }

    /// <summary>Debug: first letter of each item's ISelectable.CurrentState (Menu family).</summary>
    public string States()
    {
        if (Family != Family.Menu || Count > 40) return "";
        var s = "";
        for (var i = 0; i < Count; i++)
        {
            var item = Item(i);
            var v = ValueOf(item?.GetIl2CppType().GetProperty("CurrentState", Inst)?.GetValue(item));
            s += v == null ? "?" : v.ToString().Substring(0, 1);
        }
        return s;
    }

    // ---- il2cpp call helpers ------------------------------------------------------------------

    private static Il2CppReferenceArray<Il2Obj> Args(params Il2Obj[] a) => new(a);

    private static unsafe Il2Obj BoxDir(Dir value) =>
        new(IL2CPP.il2cpp_value_box(Il2CppClassPointerStore<Dir>.NativeClassPtr, (System.IntPtr)(&value)));

    private static unsafe Il2Obj BoxInt(int value) =>
        new(IL2CPP.il2cpp_value_box(Il2CppClassPointerStore<int>.NativeClassPtr, (System.IntPtr)(&value)));
}
