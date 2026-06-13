using System;
using HarmonyLib;
using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>Stops NGUI (UICamera) from handling mouse/touch while the F8 overlay is open.</summary>
public static class NguiInputBlockPatch
{
    private static bool _patched;

    public static void TryApply()
    {
        if (_patched)
            return;

        var uiCamera = MonoUtil.FindGameType("UICamera");
        if (MonoUtil.IsNull(uiCamera))
        {
            Plugin.Log.LogWarning("UICamera nicht gefunden — NGUI-Input-Block uebersprungen.");
            return;
        }

        var prefix = new HarmonyMethod(typeof(NguiInputBlockPatch), nameof(SkipWhenOverlayOpen));
        var names = new[] { "ProcessEvents", "ProcessMouse", "ProcessTouches", "ProcessFakeTouches" };
        var count = 0;

        for (var i = 0; i < names.Length; i++)
        {
            var method = MonoUtil.FindMethod(uiCamera, names[i]);
            if (MonoUtil.IsNull(method))
                continue;

            try
            {
                Plugin.Harmony.Patch(method, prefix: prefix);
                count++;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"NGUI-Input-Patch ({names[i]}): {ex.Message}");
            }
        }

        if (count > 0)
        {
            _patched = true;
            Plugin.Log.LogInfo($"NGUI-Input-Patches aktiv ({count}) — Overlay-Buttons bleiben klickbar.");
        }
    }

    /// <summary>Return true = run ProcessEvents. We must keep NGUI input alive for overlay +/- buttons.</summary>
    public static bool SkipWhenOverlayOpen() => true;
}
