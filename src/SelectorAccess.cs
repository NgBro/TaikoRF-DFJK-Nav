using System.Collections.Generic;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using BF = Il2CppSystem.Reflection.BindingFlags;
using Il2Obj = Il2CppSystem.Object;
using Il2Type = Il2CppSystem.Type;
using Il2Method = Il2CppSystem.Reflection.MethodInfo;
using Il2Prop = Il2CppSystem.Reflection.PropertyInfo;

namespace DfjkNav;

/// <summary>
/// Reflection wrapper over the game's generic <c>Scripts.Common.Selector.MenuSelector&lt;T&gt;</c> model,
/// reached through its view (<c>UiMenuSelector&lt;TSelector, TElement, TButton&gt; : UiView&lt;TSelector&gt;</c>).
/// Generic IL2CPP classes can't be named from C#, so everything goes through il2cpp reflection.
/// </summary>
internal sealed class SelectorAccess
{
    private const BF Inst = BF.Instance | BF.Public | BF.NonPublic;

    public readonly MonoBehaviour View;
    public readonly Il2Obj Model;
    public readonly string Name;

    private readonly Il2Method _getCurrentIndex;
    private readonly Il2Method _selectIndex;
    private readonly Il2Prop _itemList;
    private readonly Il2Prop _focus;
    private readonly Il2Prop _opened;

    private SelectorAccess(MonoBehaviour view, Il2Obj model, Il2Type modelType)
    {
        View = view;
        Model = model;
        Name = modelType.Name;
        _getCurrentIndex = modelType.GetMethod("GetCurrentIndex", Inst);
        foreach (var m in modelType.GetMethods(Inst))
        {
            if (m.Name != "Select") continue;
            var ps = m.GetParameters();
            if (ps.Length == 1 && ps[0].ParameterType.FullName == "System.Int32") { _selectIndex = m; break; }
        }
        _itemList = modelType.GetProperty("ItemList", Inst);
        _focus = modelType.GetProperty("Focus", Inst);
        _opened = modelType.GetProperty("Opened", Inst);
    }

    private bool Valid => _getCurrentIndex != null && _selectIndex != null && _itemList != null && _focus != null;

    // ---- discovery -------------------------------------------------------------------------

    private static readonly Dictionary<string, bool> IsSelectorViewCache = new();
    private static Il2CppSystem.Reflection.PropertyInfo _modelProp;

    private static bool IsSelectorView(Il2Type t)
    {
        var key = t.FullName;
        if (IsSelectorViewCache.TryGetValue(key, out var hit)) return hit;
        var result = false;
        for (var bt = t; bt != null; bt = bt.BaseType)
            if (bt.Name == "UiMenuSelector`3") { result = true; break; }
        IsSelectorViewCache[key] = result;
        return result;
    }

    private static Il2Obj GetModel(MonoBehaviour view, Il2Type t)
    {
        for (var bt = t; bt != null; bt = bt.BaseType)
        {
            if (bt.Name != "UiView`1") continue;
            var p = bt.GetProperty("Model", Inst);
            return p?.GetValue(view);
        }
        return null;
    }

    /// <summary>All selector views currently alive and enabled in the scene.</summary>
    public static List<SelectorAccess> FindAll()
    {
        var list = new List<SelectorAccess>();
        var all = Object.FindObjectsOfType(Il2CppType.Of<MonoBehaviour>());
        foreach (var o in all)
        {
            var mb = o.TryCast<MonoBehaviour>();
            if (mb == null || !mb.isActiveAndEnabled) continue;
            var t = mb.GetIl2CppType();
            if (!IsSelectorView(t)) continue;
            var model = GetModel(mb, t);
            if (model == null) continue;
            var acc = new SelectorAccess(mb, model, model.GetIl2CppType());
            if (acc.Valid) list.Add(acc);
            else Plugin.L.LogWarning($"[sel] {acc.Name}: missing members");
        }
        return list;
    }

    // ---- state -----------------------------------------------------------------------------

    private static bool ReadReactiveBool(Il2Prop prop, Il2Obj target)
    {
        var rp = prop?.GetValue(target);
        if (rp == null) return false;
        var valueProp = rp.GetIl2CppType().GetProperty("Value", Inst);
        var v = valueProp?.GetValue(rp);
        return v != null && v.Unbox<bool>();
    }

    public bool IsFocused => ReadReactiveBool(_focus, Model);
    public bool IsOpened => _opened == null || ReadReactiveBool(_opened, Model);

    public int Count
    {
        get
        {
            var rp = _itemList.GetValue(Model);
            var list = rp?.GetIl2CppType().GetProperty("Value", Inst)?.GetValue(rp);
            var count = list?.GetIl2CppType().GetProperty("Count", Inst)?.GetValue(list);
            return count == null ? 0 : count.Unbox<int>();
        }
    }

    public int CurrentIndex =>
        _getCurrentIndex.Invoke(Model, new Il2CppReferenceArray<Il2Obj>(0)).Unbox<int>();

    public void Select(int index)
    {
        _selectIndex.Invoke(Model, new Il2CppReferenceArray<Il2Obj>(new[] { BoxInt(index) }));
    }

    private static unsafe Il2Obj BoxInt(int value) =>
        new(IL2CPP.il2cpp_value_box(Il2CppClassPointerStore<int>.NativeClassPtr, (System.IntPtr)(&value)));
}
