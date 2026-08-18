using UnityEngine;

namespace MonsterCampHelper.Ingame;

/// <summary>Blocks clicks on the dimmed area outside the panel (panel controls stay clickable).</summary>
public static class OverlayInput
{
    private static GUIStyle? _blockerStyle;

    /// <summary>Call after panel IMGUI controls were drawn (reverse hit-test order).</summary>
    public static void BlockBackdropClicksOutsidePanel(float px, float py, float pw, float ph)
    {
        if (!Plugin.OverlayOpen)
            return;

        var e = Event.current;
        if (e == null)
            return;

        if (e.type != EventType.MouseDown && e.type != EventType.MouseUp
            && e.type != EventType.MouseDrag && e.type != EventType.ScrollWheel)
            return;

        var panel = new Rect(px, py, pw, ph);
        if (panel.Contains(GuiMouse(e)))
            return;

        EnsureBlockerStyle();
        DrawBackdropBlockers(px, py, pw, ph);
        e.Use();
    }

    private static void DrawBackdropBlockers(float px, float py, float pw, float ph)
    {
        var sw = Screen.width > 100 ? Screen.width : 1280;
        var sh = Screen.height > 100 ? Screen.height : 720;
        var style = _blockerStyle!;

        var prevDepth = GUI.depth;
        GUI.depth = 99995;

        if (py > 0f)
            GUI.Button(new Rect(0f, 0f, sw, py), GUIContent.none, style);
        if (py + ph < sh)
            GUI.Button(new Rect(0f, py + ph, sw, sh - py - ph), GUIContent.none, style);
        if (px > 0f)
            GUI.Button(new Rect(0f, py, px, ph), GUIContent.none, style);
        if (px + pw < sw)
            GUI.Button(new Rect(px + pw, py, sw - px - pw, ph), GUIContent.none, style);

        GUI.depth = prevDepth;
    }

    private static Vector2 GuiMouse(Event e)
    {
        return new Vector2(e.mousePosition.x, Screen.height - e.mousePosition.y);
    }

    private static void EnsureBlockerStyle()
    {
        if (_blockerStyle != null)
            return;

        _blockerStyle = new GUIStyle(GUI.skin.button);
        _blockerStyle.normal.background = null;
        _blockerStyle.hover.background = null;
        _blockerStyle.active.background = null;
        _blockerStyle.border = new RectOffset(0, 0, 0, 0);
        _blockerStyle.margin = new RectOffset(0, 0, 0, 0);
        _blockerStyle.padding = new RectOffset(0, 0, 0, 0);
    }
}
