using Il2CppInterop.Runtime;
using Scripts.OutGame.SongSelect;
using UnityEngine;

namespace DfjkNav;

/// <summary>
/// Song select's genre column (FilterScroller) has no button of its own on a keyboard; the game moves
/// focus there through its scene controller. The "Genre" extra calls that directly.
/// </summary>
internal static class SongSelectHooks
{
    private static SongSelectThunderShrineSceneObjects _objects;

    private static SongSelectSceneUiController Controller()
    {
        if (_objects == null)
        {
            var o = Object.FindObjectOfType(Il2CppType.Of<SongSelectThunderShrineSceneObjects>());
            _objects = o?.TryCast<SongSelectThunderShrineSceneObjects>();
        }
        return _objects?.uiController;
    }

    public static void OnSceneChanged() => _objects = null;

    /// <summary>Offer the Genre slot while the song list itself is what the player is moving through.</summary>
    public static bool GenreAvailable(ICursor active) =>
        active != null && active.Name == "SongScroller" && NavBehaviour.Scene == "SongSelect" && Controller() != null;

    public static void FocusGenres()
    {
        var c = Controller();
        if (c == null) { Plugin.L.LogWarning("[nav] no song select controller"); return; }
        c.SetFocus(Focuses.Filters);
    }
}
