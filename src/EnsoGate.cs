using Il2CppInterop.Runtime;
using Scripts.EnsoGame.Pause;
using UnityEngine;
using State = EnsoGameManager.State;

namespace DfjkNav;

/// <summary>
/// Keeps the mod out of the way while a song is being played: D/F/J/K are drum hits then.
/// Inside a gameplay scene, menu navigation only runs on the pause menu and once the song is over
/// (results, rewards). Everything here is cached so the per-frame cost during play is two reads.
/// </summary>
internal static class EnsoGate
{
    private static EnsoGameManager _mgr;
    private static UiPauseMenu _pause;
    private static float _nextFind;
    private static bool _noManager;

    public static void OnSceneChanged()
    {
        _mgr = null;
        _pause = null;
        _noManager = false;
        _nextFind = 0f;
    }

    /// <summary>True while a song is being played and no pause menu is open.</summary>
    public static bool Playing
    {
        get
        {
            if (_noManager) return false;
            Refresh();
            if (_mgr == null) return NavBehaviour.Scene.StartsWith("Enso"); // stay safe until found
            if (_pause != null && _pause.isActiveAndEnabled) return false;
            var st = _mgr.state;
            return st < State.ToResult || st == State.Error;
        }
    }

    private static void Refresh()
    {
        if (_mgr != null && _pause != null) return;
        if (Time.unscaledTime < _nextFind) return;
        _nextFind = Time.unscaledTime + 1f;

        if (_mgr == null)
        {
            var m = Object.FindObjectOfType(Il2CppType.Of<EnsoGameManager>());
            _mgr = m?.TryCast<EnsoGameManager>();
            if (_mgr == null && !NavBehaviour.Scene.StartsWith("Enso")) { _noManager = true; return; }
        }
        if (_pause == null)
        {
            // Inactive objects too: the pause menu exists disabled until opened.
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<UiPauseMenu>()))
            {
                var p = o.TryCast<UiPauseMenu>();
                if (p != null && p.gameObject.scene.IsValid()) { _pause = p; break; }
            }
        }
    }
}
