using System;
using HarmonyLib;
using UnityEngine;

namespace MonsterPromHelper.Ingame;

public static class NguiOverlayPatch
{
    private static bool _patched;
    private static bool _hookComponents;
    private static bool _hookLogged;

    public static bool UsesNguiHook => _patched || _hookComponents;

    public static void TryApply()
    {
        if (_patched || _hookComponents)
            return;

        var uiCamera = MonoUtil.FindGameType("UICamera");
        if (MonoUtil.IsNull(uiCamera))
        {
            Plugin.Log.LogWarning("UICamera-Typ nicht gefunden.");
            OverlayHost.EnsureExists();
            return;
        }

        if (TryHarmonyPatch(uiCamera!))
            return;

        Plugin.Log.LogWarning("Harmony-Patch fehlgeschlagen — nutze OverlayHost fuer IMGUI.");
        OverlayHost.EnsureExists();
    }

    private static bool TryHarmonyPatch(Type uiCamera)
    {
        var onGui = MonoUtil.FindMethod(uiCamera, "OnGUI");
        if (MonoUtil.IsNull(onGui))
            return false;

        try
        {
            Plugin.Harmony.Patch(
                onGui,
                postfix: new HarmonyMethod(typeof(NguiOverlayPatch), nameof(AfterNguiOnGui)));
            _patched = true;
            Plugin.Log.LogInfo("Overlay zeichnet nach NGUI (Harmony/UICamera.OnGUI).");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Harmony: {ex.Message}");
            return false;
        }
    }

    public static void AttachHooksToUiCameras(Type uiCamera)
    {
        var cameras = UnityEngine.Object.FindObjectsOfType(uiCamera);
        if (cameras == null || cameras.Length == 0)
        {
            Plugin.Log.LogWarning("Kein UICamera in der Szene — versuche beim naechsten Frame.");
            return;
        }

        var count = 0;
        for (var i = 0; i < cameras.Length; i++)
        {
            var cam = cameras[i] as Component;
            if (MonoUtil.IsNull(cam))
                continue;

            var go = cam.gameObject;
            if (go.GetComponent(typeof(OverlayDrawHook)) != null)
                continue;

            if (go.GetComponent(typeof(OverlayDrawHook)) == null)
                go.AddComponent(typeof(OverlayDrawHook));
            count++;
        }

        if (count > 0 && !_hookLogged)
        {
            _hookLogged = true;
            Plugin.Log.LogInfo($"OverlayDrawHook an {count} UICamera(s).");
        }
    }

    public static void AfterNguiOnGui()
    {
        // IMGUI overlay is drawn by OverlayHost (after all OnGUI, including NGUI).
    }
}
