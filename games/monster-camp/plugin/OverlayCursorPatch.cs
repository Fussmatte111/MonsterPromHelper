using System;
using System.Reflection;
using HarmonyLib;

namespace MonsterCampHelper.Ingame;

/// <summary>Prevents the game from hiding the cursor while the overlay is open.</summary>
public static class OverlayCursorPatch
{
    private static bool _patched;

    public static void TryApply()
    {
        if (_patched)
            return;

        var gmType = AccessTools.TypeByName("BeautifulGlitch.GeneralManager");
        if (gmType == null)
            return;

        var prefixBlockHide = new HarmonyMethod(typeof(OverlayCursorPatch), nameof(BlockHideWhileOverlay));

        if (TryPatch(gmType, "SetCursorVisible", prefixBlockHide) > 0)
        {
            _patched = true;
            Plugin.Log.LogInfo("Overlay cursor patch active (SetCursorVisible).");
        }
    }

    private static int TryPatch(Type type, string name, HarmonyMethod prefix)
    {
        var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            return 0;

        try
        {
            Plugin.Harmony.Patch(method, prefix: prefix);
            return 1;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning("Cursor patch (" + name + "): " + ex.Message);
            return 0;
        }
    }

    // Parameter name must match game method: SetCursorVisible(bool isVisible)
    public static bool BlockHideWhileOverlay(bool isVisible)
    {
        if (Plugin.OverlayOpen && !isVisible)
            return false;
        return true;
    }
}
