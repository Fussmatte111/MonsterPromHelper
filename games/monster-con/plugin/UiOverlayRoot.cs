using UnityEngine;
using UnityEngine.UI;

namespace MonsterProm4Helper.Ingame;

/// <summary>Screen-space Canvas above NGUI (MP4 renders NGUI after IMGUI — uGUI works).</summary>
public static class UiOverlayRoot
{
    private static Canvas? _canvas;
    private static RectTransform? _root;

    public static RectTransform Root
    {
        get
        {
            Ensure();
            return _root!;
        }
    }

    public static void Ensure()
    {
        if (_canvas != null && _root != null)
            return;

        var go = new GameObject("MPHelper_UiRoot");
        Object.DontDestroyOnLoad(go);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        _root = go.GetComponent<RectTransform>();
    }

    public static void DestroyAll()
    {
        if (_canvas == null)
            return;
        Object.Destroy(_canvas.gameObject);
        _canvas = null;
        _root = null;
    }
}
