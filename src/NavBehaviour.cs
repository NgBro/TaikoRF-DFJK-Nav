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

    private void Update()
    {
        Frame = Time.frameCount;

        var name = SceneManager.GetActiveScene().name;
        if (name != Scene)
        {
            Plugin.L.LogInfo($"[scene] {Scene} -> {name}");
            Scene = name;
            ProbePatches.ResetScene();
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
        BackButton.Set(Nav.BackVisible, Nav.BackFocused);
    }
}
