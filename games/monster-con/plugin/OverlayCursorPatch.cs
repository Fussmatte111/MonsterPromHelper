using System;
using System.Reflection;
using Game;
using HarmonyLib;

namespace MonsterProm4Helper.Ingame;

/// <summary>Prevents the game from hiding the cursor while the overlay is open (if hooks exist).</summary>
public static class OverlayCursorPatch
{
    private static bool _patched;

    public static void TryApply()
    {
        if (_patched)
            return;

        var gmType = typeof(GeneralManager);
        var prefixBlockHide = new HarmonyMethod(typeof(OverlayCursorPatch), nameof(BlockHideWhileOverlay));
        var prefixCheck = new HarmonyMethod(typeof(OverlayCursorPatch), nameof(SkipCheckWhileOverlay));

        var count = 0;
        count += TryPatch(gmType, "SetCursorVisible", prefixBlockHide);
        count += TryPatch(gmType, "SetCursorVisible_Local", prefixBlockHide);
        count += TryPatch(gmType, "CheckCursor", prefixCheck);

        if (count > 0)
        {
            _patched = true;
            Plugin.Log.LogInfo("Overlay cursor patches active (" + count + ").");
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

    public static bool BlockHideWhileOverlay(bool visible)
    {
        if (Plugin.OverlayOpen && !visible)
            return false;
        return true;
    }

    public static bool SkipCheckWhileOverlay()
    {
        if (!Plugin.OverlayOpen)
            return true;

        OverlayCursor.EnsureVisible();
        return false;
    }
}
