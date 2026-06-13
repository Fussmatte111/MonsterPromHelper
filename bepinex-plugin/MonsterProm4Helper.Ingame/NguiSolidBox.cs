using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>Reliable solid NGUI quads via UITexture (UI2DSprite clones were invisible).</summary>
internal static class NguiSolidBox
{
    private static Texture2D? _whiteTex;

    public static UITexture Add(Transform parent, int depth, int width, int height, Color color)
    {
        var box = NGUITools.AddWidget<UITexture>(parent.gameObject, depth);
        box.mainTexture = GetWhiteTexture();
        box.width = width;
        box.height = height;
        box.color = color;
        box.depth = depth;
        return box;
    }

    public static GameObject Create(string name, Transform parent, int depth, int width, int height, Color color)
    {
        var go = new GameObject(name);
        go.transform.parent = parent;
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;
        Add(go.transform, depth, width, height, color);
        return go;
    }

    public static void SetColor(GameObject go, Color color)
    {
        var tex = go.GetComponentInChildren<UITexture>();
        if (tex != null)
            tex.color = color;
    }

    private static Texture2D GetWhiteTexture()
    {
        if (_whiteTex != null)
            return _whiteTex;

        _whiteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        _whiteTex.name = "MPHelper_WhiteTex";
        _whiteTex.wrapMode = TextureWrapMode.Clamp;
        _whiteTex.filterMode = FilterMode.Bilinear;

        var pixels = new Color[16];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        _whiteTex.SetPixels(pixels);
        _whiteTex.Apply(false, true);
        return _whiteTex;
    }
}
