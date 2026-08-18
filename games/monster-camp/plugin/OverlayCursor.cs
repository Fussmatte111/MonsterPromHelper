using UnityEngine;

namespace MonsterCampHelper.Ingame;

/// <summary>Shows the OS cursor while the F8 overlay is open.</summary>
public static class OverlayCursor
{
    public static void EnsureVisible()
    {
        if (!Plugin.OverlayOpen)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
