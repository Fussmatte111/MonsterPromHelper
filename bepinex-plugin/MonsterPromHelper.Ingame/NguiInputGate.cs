using System.Collections.Generic;
using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Disables UICamera mouse/touch while overlay is open (works even if Harmony patches fail).</summary>
public static class NguiInputGate
{
    private sealed class SavedState
    {
        public bool UseMouse;
        public bool UseTouch;
    }

    private static readonly Dictionary<UICamera, SavedState> Saved = new Dictionary<UICamera, SavedState>();
    private static float _nextRescan;

    public static void Sync()
    {
        if (!Plugin.OverlayOpen)
        {
            Restore();
            return;
        }

        if (Time.unscaledTime >= _nextRescan)
        {
            _nextRescan = Time.unscaledTime + 2f;
            var cameras = Object.FindObjectsOfType<UICamera>();
            for (var i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || Saved.ContainsKey(cam))
                    continue;
                Saved[cam] = new SavedState { UseMouse = cam.useMouse, UseTouch = cam.useTouch };
            }
        }

        foreach (var kv in Saved)
        {
            if (kv.Key == null)
                continue;
            kv.Key.useMouse = false;
            kv.Key.useTouch = false;
        }
    }

    public static void Restore()
    {
        foreach (var kv in Saved)
        {
            if (kv.Key == null)
                continue;
            kv.Key.useMouse = kv.Value.UseMouse;
            kv.Key.useTouch = kv.Value.UseTouch;
        }

        Saved.Clear();
        _nextRescan = 0f;
    }
}
