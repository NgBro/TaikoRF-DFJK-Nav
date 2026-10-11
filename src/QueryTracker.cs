using UnityEngine;

namespace DfjkNav;

/// <summary>Remembers when the current screen last polled certain inputs, to decide which extras make sense.</summary>
internal static class QueryTracker
{
    public const int Tab = 0, Horizontal = 1, Vertical = 2;
    private static readonly int[] LastFrame = { -1000, -1000, -1000 };

    public static void Asked(int what) => LastFrame[what] = Time.frameCount;

    public static bool RecentlyAsked(int what) => Time.frameCount - LastFrame[what] < 30;

    /// <summary>Record a direction query; an axis-less query counts for both axes.</summary>
    public static void AskedDir(ControllerManager.LRType lr)
    {
        if (lr != ControllerManager.LRType.Vertical) Asked(Horizontal);
        if (lr != ControllerManager.LRType.Horizontal) Asked(Vertical);
    }
}
