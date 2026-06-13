using UnityEngine;
using UnityEngine.UI;

namespace MonsterProm4Helper.Ingame;

/// <summary>Visible MP4 overlay panel (uGUI) — MP1-style content, works above NGUI.</summary>
public static class UiOverlayPanel
{
    private const float PanelW = 420f;
    private const float PanelH = 520f;

    private static GameObject? _dimmer;
    private static GameObject? _panelGo;
    private static Text? _bodyText;
    private static Text? _hudText;
    private static GameObject? _toastGo;
    private static Text? _toastTitle;
    private static Text? _toastBody;
    private static GameObject? _statsGo;
    private static readonly StatRowUi[] StatRows = new StatRowUi[EventDb.StatKeys.Length];
    private static bool _built;

    private sealed class StatRowUi
    {
        public string Key = "";
        public Text? ValueText;
    }

    public static void EnsureBuilt()
    {
        if (_built)
            return;

        UiOverlayRoot.Ensure();
        var root = UiOverlayRoot.Root;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        _dimmer = CreateStretch("Dimmer", root);
        var dimImg = _dimmer.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.35f);
        dimImg.raycastTarget = true;
        _dimmer.SetActive(false);

        _panelGo = new GameObject("Panel", typeof(RectTransform));
        _panelGo.transform.SetParent(root, false);
        var panelRt = _panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(PanelW, PanelH);
        var panelImg = _panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.07f, 0.09f, 0.16f, 0.96f);

        var scrollGo = CreateStretch("Scroll", _panelGo.transform);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.offsetMin = new Vector2(10f, 110f);
        scrollRt.offsetMax = new Vector2(-10f, -10f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = CreateStretch("Viewport", scrollGo.transform);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 800f);

        _bodyText = CreateText("Body", content.transform, font, 13, Color.white);
        var bodyRt = _bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 1f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.offsetMin = new Vector2(4f, -800f);
        bodyRt.offsetMax = new Vector2(-4f, 0f);
        _bodyText.supportRichText = true;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        scroll.content = contentRt;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        _statsGo = CreateStretch("Stats", _panelGo.transform);
        var statsRt = _statsGo.GetComponent<RectTransform>();
        statsRt.anchorMin = new Vector2(0f, 0f);
        statsRt.anchorMax = new Vector2(1f, 0f);
        statsRt.pivot = new Vector2(0.5f, 0f);
        statsRt.sizeDelta = new Vector2(0f, 100f);
        statsRt.anchoredPosition = Vector2.zero;
        BuildStatRows(_statsGo.transform, font);

        _panelGo.SetActive(false);

        _hudText = CreateText("Hud", root, font, 14, new Color(0.85f, 1f, 0.9f));
        var hudRt = _hudText.rectTransform;
        hudRt.anchorMin = new Vector2(0f, 1f);
        hudRt.anchorMax = new Vector2(0f, 1f);
        hudRt.pivot = new Vector2(0f, 1f);
        hudRt.anchoredPosition = new Vector2(12f, -12f);
        hudRt.sizeDelta = new Vector2(320f, 28f);
        _hudText.text = "F8 = Overlay  |  F9 = Alt";

        _toastGo = new GameObject("Toast", typeof(RectTransform));
        _toastGo.transform.SetParent(root, false);
        var toastRt = _toastGo.GetComponent<RectTransform>();
        toastRt.anchorMin = new Vector2(1f, 0f);
        toastRt.anchorMax = new Vector2(1f, 0f);
        toastRt.pivot = new Vector2(1f, 0f);
        toastRt.anchoredPosition = new Vector2(-20f, 20f);
        toastRt.sizeDelta = new Vector2(300f, 72f);
        _toastGo.AddComponent<Image>().color = new Color(0.1f, 0.38f, 0.2f, 0.94f);
        _toastTitle = CreateText("ToastTitle", _toastGo.transform, font, 15, new Color(0.85f, 1f, 0.9f));
        _toastTitle.rectTransform.anchoredPosition = new Vector2(10f, -10f);
        _toastTitle.rectTransform.sizeDelta = new Vector2(280f, 22f);
        _toastBody = CreateText("ToastBody", _toastGo.transform, font, 12, Color.white);
        _toastBody.rectTransform.anchoredPosition = new Vector2(10f, -34f);
        _toastBody.rectTransform.sizeDelta = new Vector2(280f, 34f);
        _toastGo.SetActive(false);

        _built = true;
        Plugin.Log.LogInfo("uGUI-Overlay bereit (wie MP1, sichtbar ueber NGUI).");
    }

    public static void Sync(bool overlayOpen, bool showHud, OverlayViewModel vm, LiveGameState live, GameBridge bridge)
    {
        EnsureBuilt();
        if (_dimmer == null || _panelGo == null || _bodyText == null)
            return;

        if (_hudText != null)
            _hudText.gameObject.SetActive(showHud && !overlayOpen);

        _dimmer.SetActive(overlayOpen);
        _panelGo.SetActive(overlayOpen);

        if (!overlayOpen)
            return;

        _bodyText.text = OverlayTextFormatter.FormatMain(vm, live);
        var pref = _bodyText.preferredHeight + 40f;
        if (pref < 300f)
            pref = 300f;
        _bodyText.rectTransform.offsetMin = new Vector2(4f, -pref);

        SyncStatRows(live, live.InSchool);
    }

    public static void ShowPickHint(string title, string body, string sub)
    {
        EnsureBuilt();
        if (_toastGo == null || _toastTitle == null || _toastBody == null || Plugin.OverlayOpen)
        {
            HidePickHint();
            return;
        }

        var bg = _toastGo.GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0.1f, 0.38f, 0.2f, 0.94f);
        _toastGo.SetActive(true);
        _toastTitle.text = title;
        _toastBody.text = MonoUtil.HasText(sub) ? body + "\n" + sub : body;
    }

    public static void HidePickHint() { if (_toastGo != null) _toastGo.SetActive(false); }

    public static void ShowSecretToast(string title, string body)
    {
        EnsureBuilt();
        if (_toastGo == null || _toastTitle == null || _toastBody == null || Plugin.OverlayOpen)
            return;
        var bg = _toastGo.GetComponent<Image>();
        if (bg != null)
            bg.color = new Color(0.5f, 0.1f, 0.42f, 0.96f);
        _toastGo.SetActive(true);
        _toastTitle.text = title;
        _toastBody.text = body;
    }

    public static void HideSecretToast() { if (_toastGo != null) _toastGo.SetActive(false); }

    private static void BuildStatRows(Transform parent, Font font)
    {
        var title = CreateText("StatsTitle", parent, font, 12, new Color(0.75f, 0.8f, 0.85f));
        title.text = "Stats (+/- nur in MainGame-Runde):";
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(8f, -22f);
        title.rectTransform.offsetMax = new Vector2(-8f, 0f);

        for (var i = 0; i < EventDb.StatKeys.Length; i++)
        {
            var key = EventDb.StatKeys[i];
            var y = -28f - i * 26f;
            var row = new GameObject("Row_" + key, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(8f, y - 24f);
            rt.offsetMax = new Vector2(-8f, y);

            var label = CreateText("L", row.transform, font, 12, Color.white);
            label.text = key;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(0.35f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            var val = CreateText("V", row.transform, font, 12, Color.white);
            val.rectTransform.anchorMin = new Vector2(0.35f, 0f);
            val.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            val.rectTransform.offsetMin = Vector2.zero;
            val.rectTransform.offsetMax = Vector2.zero;

            var keyCopy = key;
            CreateMiniButton(row.transform, font, "-", 0.78f, () => Plugin.Instance?.NudgeStat(keyCopy, -1));
            CreateMiniButton(row.transform, font, "+", 0.88f, () => Plugin.Instance?.NudgeStat(keyCopy, 1));

            StatRows[i] = new StatRowUi { Key = key, ValueText = val };
        }
    }

    private static void SyncStatRows(LiveGameState live, bool editable)
    {
        if (_statsGo == null)
            return;
        _statsGo.SetActive(editable);
        if (!editable)
            return;
        for (var i = 0; i < StatRows.Length; i++)
        {
            if (StatRows[i].ValueText == null)
                continue;
            StatRows[i].ValueText.text = live.Stats.TryGetValue(StatRows[i].Key, out var v) ? v.ToString() : "0";
        }
    }

    private static GameObject CreateStretch(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAnchor.UpperLeft;
        return t;
    }

    private static void CreateMiniButton(Transform parent, Font font, string label, float anchorX, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchorX, 0.1f);
        rt.anchorMax = new Vector2(anchorX + 0.08f, 0.9f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.32f, 0.5f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var t = CreateText("T", go.transform, font, 13, Color.white);
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }
}
