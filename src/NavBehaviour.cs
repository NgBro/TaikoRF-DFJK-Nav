using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DfjkNav;

public class NavBehaviour : MonoBehaviour
{
    public NavBehaviour(IntPtr ptr) : base(ptr) { }

    internal static string Scene = "";
    internal static int Frame;

    private static readonly Key[] Watched =
    {
        Key.D, Key.F, Key.J, Key.K, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow,
        Key.Enter, Key.Space, Key.Escape, Key.Backspace,
    };

    private static readonly System.Collections.Generic.List<string> Labels = new();

    private void Update()
    {
        Frame = Time.frameCount;

        var name = SceneManager.GetActiveScene().name;
        if (name != Scene)
        {
            Plugin.L.LogInfo($"[scene] {Scene} -> {name}");
            Scene = name;
            ProbePatches.ResetScene();
            EnsoGate.OnSceneChanged();
            SongSelectHooks.OnSceneChanged();
            Nav.OnSceneChanged();
        }

        if (Plugin.Probe.Value)
        {
            var kb = Keyboard.current;
            if (kb != null)
                foreach (var k in Watched)
                    if (kb[k].wasPressedThisFrame)
                        Plugin.L.LogInfo($"[key] f{Frame} {k} down");
        }

        _ = Nav.Current; // make sure selector moves happen even if the game asks nothing this frame

        if (!Nav.OverlayVisible) { Overlay.Show(null, -1); return; }
        Labels.Clear();
        if (Nav.Editing)
        {
            Labels.Add("D ◀  adjust  ▶ K");
            Labels.Add("F/J: done");
            Overlay.Show(Labels, 0, editing: true);
            return;
        }
        foreach (var e in Nav.CurrentExtras) Labels.Add(e.Label);
        Overlay.Show(Labels, Nav.ExtraFocus);
    }
}
