using System.Reflection;
using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Shows the OS cursor while the F8 overlay is open (game normally hides it via NGUI/GeneralManager).</summary>
public static class OverlayCursor
{
    private static readonly BindingFlags Inst =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static FieldInfo? _forceHideField;

    public static void EnsureVisible()
    {
        if (!Plugin.OverlayOpen)
            return;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        var gm = GeneralManager.Instance;
        if (gm == null)
            return;

        try
        {
            ClearForceHide(gm);
            gm.SetCursorVisible(true);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning("Overlay cursor: " + ex.Message);
        }
    }

    private static void ClearForceHide(GeneralManager gm)
    {
        if (_forceHideField == null)
        {
            _forceHideField = typeof(GeneralManager).GetField("mForceHideCursor", Inst);
            if (_forceHideField == null)
                return;
        }

        _forceHideField.SetValue(gm, false);
    }
}
