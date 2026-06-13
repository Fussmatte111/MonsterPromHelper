using System.Text;
using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Compact IMGUI panel (top-right) with highlighted recommendation.</summary>
public static class OverlayGui
{
    public const float PanelW = 420f;
    private const float PanelHMax = 520f;

    private static Vector2 _panelScroll;
    private static float _contentHeight = 900f;
    private const float Margin = 16f;

    private static Texture2D? _dimTex;
    private static Texture2D? _panelTex;
    private static Texture2D? _pickTex;
    private static Texture2D? _secretTex;
    private static Texture2D? _optionTex;
    private static Texture2D? _optionDimTex;
    private static GUIStyle? _titleStyle;
    private static GUIStyle? _bodyStyle;
    private static GUIStyle? _mutedStyle;
    private static GUIStyle? _pickStyle;
    private static GUIStyle? _secretStyle;
    private static GUIStyle? _optionPickStyle;
    private static GUIStyle? _optionStyle;
    private static bool _stylesReady;

    public static GUIStyle TitleStyle { get { EnsureStyles(); return _titleStyle!; } }
    public static GUIStyle BodyStyle { get { EnsureStyles(); return _bodyStyle!; } }
    public static GUIStyle MutedStyle { get { EnsureStyles(); return _mutedStyle!; } }
    public static GUIStyle PickStyle { get { EnsureStyles(); return _pickStyle!; } }

    private static Texture2D? _toastTex;
    private static Texture2D? _pickHintTex;
    private static GUIStyle? _toastTitleStyle;
    private static GUIStyle? _toastBodyStyle;
    private static GUIStyle? _pickHintTitleStyle;
    private static GUIStyle? _pickHintBodyStyle;

    public static void DrawPickHint(string title, string body, string sub)
    {
        EnsureStyles();

        GUI.depth = 100001;
        GUI.color = Color.white;

        var sw = Screen.width > 100 ? Screen.width : 1280;
        var sh = Screen.height > 100 ? Screen.height : 720;
        var w = 300f;
        var h = MonoUtil.HasText(sub) ? 68f : 52f;
        var x = sw - w - 20f;
        var y = sh - h - 24f;

        if (_pickHintTex == null)
            _pickHintTex = MakeTex(new Color(0.1f, 0.38f, 0.2f, 0.94f));

        if (_pickHintTitleStyle == null)
        {
            _pickHintTitleStyle = new GUIStyle(GUI.skin.label);
            _pickHintTitleStyle.fontSize = 15;
            _pickHintTitleStyle.fontStyle = FontStyle.Bold;
            _pickHintTitleStyle.normal.textColor = new Color(0.85f, 1f, 0.9f);
        }

        if (_pickHintBodyStyle == null)
        {
            _pickHintBodyStyle = new GUIStyle(GUI.skin.label);
            _pickHintBodyStyle.fontSize = 12;
            _pickHintBodyStyle.normal.textColor = Color.white;
        }

        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(new Rect(x, y, w, h), _pickHintTex);

        GUI.Label(new Rect(x + 10, y + 6, w - 20, 22f), title, _pickHintTitleStyle);
        GUI.Label(new Rect(x + 10, y + 26, w - 20, 20f), body, _pickHintBodyStyle);
        if (MonoUtil.HasText(sub))
            GUI.Label(new Rect(x + 10, y + 44, w - 20, 18f), sub, _mutedStyle!);
    }

    public static void DrawSecretToast(string title, string body)
    {
        var ev = Event.current;
        if (ev.type != EventType.Repaint && ev.type != EventType.Layout)
            return;

        EnsureStyles();

        GUI.depth = 100001;
        GUI.color = Color.white;

        var sw = Screen.width > 100 ? Screen.width : 1280;
        var w = 440f;
        var lineCount = 1;
        if (MonoUtil.HasText(body))
        {
            for (var i = 0; i < body.Length; i++)
            {
                if (body[i] == '\n')
                    lineCount++;
            }
        }

        var h = 36f + lineCount * 18f;
        if (h < 80f)
            h = 80f;
        if (h > 130f)
            h = 130f;
        var x = (sw - w) * 0.5f;
        var y = 24f;

        if (_toastTex == null)
            _toastTex = MakeTex(new Color(0.5f, 0.1f, 0.42f, 0.96f));

        if (_toastTitleStyle == null)
        {
            _toastTitleStyle = new GUIStyle(GUI.skin.label);
            _toastTitleStyle.fontSize = 14;
            _toastTitleStyle.fontStyle = FontStyle.Bold;
            _toastTitleStyle.normal.textColor = new Color(1f, 0.9f, 0.98f);
        }

        if (_toastBodyStyle == null)
        {
            _toastBodyStyle = new GUIStyle(GUI.skin.label);
            _toastBodyStyle.fontSize = 11;
            _toastBodyStyle.wordWrap = true;
            _toastBodyStyle.normal.textColor = Color.white;
        }

        GUI.DrawTexture(new Rect(x, y, w, h), _toastTex);
        GUI.Label(new Rect(x + 12, y + 8, w - 24, 22f), title, _toastTitleStyle);
        GUI.Label(new Rect(x + 12, y + 28, w - 24, h - 36f), body, _toastBodyStyle);
    }

    public static void DrawHud(float untilTime, bool overlayOpen)
    {
        if (overlayOpen || Time.unscaledTime >= untilTime)
            return;

        EnsureStyles();
        GUI.depth = 100000;
        GUI.color = Color.white;
        GUI.Box(new Rect(8, 8, 280, 24), "F8 = Overlay");
    }

    public static void GetPanelRect(out float px, out float py, out float pw, out float ph)
    {
        var sw = Screen.width > 100 ? Screen.width : 1280;
        var sh = Screen.height > 100 ? Screen.height : 720;
        pw = PanelW;
        ph = PanelHMax;
        if (ph > sh - Margin * 2f)
            ph = sh - Margin * 2f;
        px = (sw - pw) * 0.5f;
        py = (sh - ph) * 0.5f;
    }

    public static void ApplyScrollWheel(float panelX, float panelY, float panelH)
    {
        var wheel = Input.GetAxis("Mouse ScrollWheel");
        if (wheel == 0f)
            return;

        var mp = Input.mousePosition;
        var mx = mp.x;
        var my = Screen.height - mp.y;
        if (mx < panelX || mx > panelX + PanelW || my < panelY || my > panelY + panelH)
            return;

        _panelScroll.y = Mathf.Max(0f, _panelScroll.y + wheel * 80f);
    }

    public static void DrawFull(OverlayViewModel vm, LiveGameState live, GameBridge bridge)
    {
        EnsureStyles();

        GUI.depth = 100000;
        GUI.color = Color.white;

        var sw = Screen.width > 100 ? Screen.width : 1280;
        var sh = Screen.height > 100 ? Screen.height : 720;
        GetPanelRect(out var px, out var py, out var panelW, out var panelH);

        OverlayCursor.EnsureVisible();

        var isRepaint = Event.current.type == EventType.Repaint;

        if (isRepaint)
        {
            GUI.DrawTexture(new Rect(0, 0, sw, sh), _dimTex!);
            GUI.DrawTexture(new Rect(px - 2, py - 2, panelW + 4, panelH + 4), _panelTex!);
        }

        var innerW = panelW - 20f;
        GUI.BeginGroup(new Rect(px + 10f, py + 8f, innerW, panelH - 16f));
        _panelScroll = GUI.BeginScrollView(
            new Rect(0, 0, innerW, panelH - 16f),
            _panelScroll,
            new Rect(0, 0, innerW - 16f, _contentHeight));

        var y = 0f;

        GUI.Label(new Rect(0, y, innerW, 22f), "Monster Prom Helper", _titleStyle!);
        y += 24f;

        if (vm.OnSecretRoute && MonoUtil.HasText(vm.SecretBanner))
        {
            if (isRepaint)
                GUI.DrawTexture(new Rect(0, y, innerW, 30f), _secretTex!);
            GUI.Label(new Rect(8, y + 5, innerW - 16, 22f), vm.SecretBanner, _secretStyle!);
            y += 36f;
        }

        if (vm.RecommendedOption > 0)
        {
            var pickH = 56f;
            if (isRepaint)
                GUI.DrawTexture(new Rect(0, y, innerW, pickH), _pickTex!);
            var pickTitle = "EMPFOHLEN: Option " + vm.RecommendedOption;
            if (MonoUtil.HasText(vm.RecommendedStat))
                pickTitle += "  (" + vm.RecommendedStat + " " + vm.RecommendedValue + ")";
            GUI.Label(new Rect(10, y + 6, innerW - 20, 22f), pickTitle, _pickStyle!);
            if (MonoUtil.HasText(vm.RecommendedHint))
            {
                var hint = Truncate(vm.RecommendedHint, 70);
                GUI.Label(new Rect(10, y + 28, innerW - 20, 22f), hint, _mutedStyle!);
            }
            y += pickH + 8f;
        }
        else if (vm.EventActive && vm.DbOptions.Count > 0)
        {
            GUI.Label(new Rect(0, y, innerW, 18f), "Kein klarer Stat-Vorteil", _mutedStyle!);
            y += 22f;
        }

        if (vm.EnginePick > 0)
        {
            GUI.Label(new Rect(0, y, innerW, 18f),
                "Spiel waehlt: Option " + vm.EnginePick + " (" + vm.EngineStat + ")", _mutedStyle!);
            y += 20f;
        }

        GUI.Label(new Rect(0, y, innerW, 18f), vm.Status, _bodyStyle!);
        y += 20f;

        if (MonoUtil.HasText(vm.EventName))
        {
            var evLine = vm.EventName;
            if (MonoUtil.HasText(vm.Route))
                evLine += "  |  " + vm.Route;
            GUI.Label(new Rect(0, y, innerW, 18f), Truncate(evLine, 52), _bodyStyle!);
            y += 20f;
        }

        if (MonoUtil.HasText(vm.StatsLine))
        {
            GUI.Label(new Rect(0, y, innerW, 16f), Truncate(vm.StatsLine, 58), _mutedStyle!);
            y += 18f;
        }

        if (MonoUtil.HasText(vm.DialogText))
        {
            y += 4f;
            GUI.Label(new Rect(0, y, innerW, 16f), "Dialog", _mutedStyle!);
            y += 18f;
            GUI.Label(new Rect(0, y, innerW, 48f), Truncate(vm.DialogText, 160), _bodyStyle!);
            y += 52f;
        }

        if (MonoUtil.HasText(vm.Option1Text) || MonoUtil.HasText(vm.Option2Text))
        {
            y += 4f;
            DrawAnswerBlock(0f, ref y, innerW, 1, vm.Option1Text, vm.RecommendedOption == 1, vm.DbOptions);
            DrawAnswerBlock(0f, ref y, innerW, 2, vm.Option2Text, vm.RecommendedOption == 2, vm.DbOptions);
        }

        for (var i = 0; i < vm.DbOptions.Count; i++)
        {
            var opt = vm.DbOptions[i];
            if (opt.Option == 1 || opt.Option == 2)
                continue;

            DrawDbOptionLine(0f, ref y, innerW, opt);
        }

        if (vm.HitLines.Count > 0)
        {
            y += 6f;
            GUI.Label(new Rect(0, y, innerW, 16f), "Moegliche Events:", _mutedStyle!);
            y += 18f;
            for (var i = 0; i < vm.HitLines.Count && i < 4; i++)
            {
                GUI.Label(new Rect(0, y, innerW, 16f), Truncate(vm.HitLines[i], 50), _mutedStyle!);
                y += 17f;
            }
        }

        if (MonoUtil.HasText(vm.MatchNote))
        {
            GUI.Label(new Rect(0, y, innerW, 32f), Truncate(vm.MatchNote, 80), _mutedStyle!);
            y += 34f;
        }

        if (vm.FooterLines.Count > 0)
        {
            GUI.Label(new Rect(0, y, innerW, 18f), vm.FooterLines[0], _mutedStyle!);
            y += 22f;
        }

        RomanceStatsPanel.Draw(ref y, innerW, live, bridge, live.InSchool);

        _contentHeight = y + 24f;
        GUI.EndScrollView();
        GUI.EndGroup();

        OverlayInput.BlockBackdropClicksOutsidePanel(px, py, panelW, panelH);
    }

    private static void DrawAnswerBlock(
        float x,
        ref float y,
        float w,
        int optionNum,
        string text,
        bool isPick,
        System.Collections.Generic.List<OptionDisplayLine> dbOptions)
    {
        if (!MonoUtil.HasText(text))
            return;

        var h = 52f;
        if (Event.current.type == EventType.Repaint)
            GUI.DrawTexture(new Rect(x, y, w, h), isPick ? _optionTex! : _optionDimTex!);

        var title = isPick ? ">> OPTION " + optionNum + " <<" : "Option " + optionNum;
        GUI.Label(new Rect(x + 8, y + 4, w - 16, 18f), title, isPick ? _optionPickStyle! : _optionStyle!);
        GUI.Label(new Rect(x + 8, y + 22, w - 16, 28f), Truncate(text, 55), _bodyStyle!);

        OptionDisplayLine? dbLine = null;
        for (var i = 0; i < dbOptions.Count; i++)
        {
            if (dbOptions[i].Option == optionNum)
            {
                dbLine = dbOptions[i];
                break;
            }
        }

        if (dbLine != null)
        {
            var mark = dbLine.Verdict == "success" ? "+" : dbLine.Verdict == "tie" ? "~" : "-";
            var statLine = mark + " " + dbLine.Stat + "=" + dbLine.Value;
            if (MonoUtil.HasText(dbLine.Hint))
                statLine += "  |  " + Truncate(dbLine.Hint, 28);
            GUI.Label(new Rect(x + 8, y + 38, w - 16, 14f), Truncate(statLine, 52), _mutedStyle!);
            y += h + 6f;
        }
        else
            y += h + 6f;
    }

    private static void DrawDbOptionLine(float x, ref float y, float w, OptionDisplayLine opt)
    {
        var line = (opt.IsRecommended ? "* " : "  ") + opt.Option + ": " + opt.Stat + "=" + opt.Value;
        GUI.Label(new Rect(x, y, w, 16f), Truncate(line, 54), opt.IsRecommended ? _optionPickStyle! : _mutedStyle!);
        y += 17f;
    }

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            return text ?? "";
        return text.Substring(0, maxLen - 3) + "...";
    }

    private static void EnsureStyles()
    {
        if (_stylesReady)
            return;

        _dimTex = MakeTex(new Color(0f, 0f, 0f, 0.35f));
        _panelTex = MakeTex(new Color(0.07f, 0.09f, 0.16f, 0.96f));
        _pickTex = MakeTex(new Color(0.12f, 0.42f, 0.22f, 0.95f));
        _secretTex = MakeTex(new Color(0.55f, 0.12f, 0.45f, 0.95f));
        _optionTex = MakeTex(new Color(0.15f, 0.38f, 0.24f, 0.9f));
        _optionDimTex = MakeTex(new Color(0.14f, 0.16f, 0.22f, 0.85f));

        _titleStyle = new GUIStyle(GUI.skin.label);
        _titleStyle.fontSize = 15;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.normal.textColor = new Color(0.95f, 0.97f, 1f);

        _bodyStyle = new GUIStyle(GUI.skin.label);
        _bodyStyle.fontSize = 12;
        _bodyStyle.wordWrap = true;
        _bodyStyle.normal.textColor = Color.white;

        _mutedStyle = new GUIStyle(_bodyStyle);
        _mutedStyle.fontSize = 11;
        _mutedStyle.normal.textColor = new Color(0.72f, 0.76f, 0.82f);

        _pickStyle = new GUIStyle(_bodyStyle);
        _pickStyle.fontSize = 13;
        _pickStyle.fontStyle = FontStyle.Bold;
        _pickStyle.normal.textColor = new Color(0.75f, 1f, 0.82f);

        _secretStyle = new GUIStyle(_pickStyle);
        _secretStyle.normal.textColor = new Color(1f, 0.85f, 0.95f);

        _optionPickStyle = new GUIStyle(_pickStyle);
        _optionPickStyle.fontSize = 12;

        _optionStyle = new GUIStyle(_bodyStyle);
        _optionStyle.fontStyle = FontStyle.Bold;

        _stylesReady = true;
    }

    private static Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
