using BeautifulGlitch;
using Game;
using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>NGUI overlay — MP4 renders NGUI after IMGUI, so uGUI/IMGUI stay invisible.</summary>
public static class NguiOverlayView
{
    private const int OverlayDepth = 32000;
    private const int BodyFontSize = 22;
    private const int SmallFontSize = 18;
    private const int HudFontSize = 20;
    private const int PanelWidth = 440;
    private const int PanelHeight = 580;
    private const int BodyWidth = 400;
    private const int BodyHeight = 230;

    private static GameObject? _rootGo;
    private static GameObject? _dimGo;
    private static GameObject? _panelGo;
    private static UILabel? _label;
    private static UILabel? _hudLabel;
    private static GameObject? _toastGo;
    private static UILabel? _toastTitle;
    private static UILabel? _toastBody;
    private static GameObject? _statsGo;
    private static readonly StatRowUi[] StatRows = new StatRowUi[EventDb.StatKeys.Length];
    private static bool _built;
    private static float _nextBuildLog;

    private sealed class StatRowUi
    {
        public string Key = "";
        public UILabel? ValueLabel;
        public GameObject? MinusBtn;
        public GameObject? PlusBtn;
    }

    private static UILabel? _statsTitle;

    public static bool IsActive => _rootGo != null && _rootGo.activeInHierarchy;

    public static UILabel? FindReferenceLabelPublic() => FindReferenceLabel();

    public static void ApplyLabelFontPublic(UILabel src, UILabel dst, int fontSize) =>
        ApplyLabelFont(src, dst, fontSize);

    public static void ResetForNewScene()
    {
        DestroyWidgets();
    }

    public static void Sync(bool overlayOpen, bool showHud, OverlayViewModel vm, LiveGameState live, GameBridge bridge)
    {
        if (!EnsureBuilt())
        {
            if (Time.unscaledTime >= _nextBuildLog)
            {
                _nextBuildLog = Time.unscaledTime + 8f;
                var refLabel = FindReferenceLabel();
                var parent = FindUiParent(refLabel);
                Plugin.Log.LogWarning(
                    "NGUI wartet — Parent="
                    + (parent != null ? parent.name : "nein")
                    + ", UILabel="
                    + (refLabel != null ? refLabel.name : "nein")
                    + ", Szene="
                    + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            return;
        }

        if (_hudLabel != null)
            _hudLabel.gameObject.SetActive(showHud && !overlayOpen);

        if (_dimGo != null)
            _dimGo.SetActive(overlayOpen);
        if (_panelGo != null)
            _panelGo.SetActive(overlayOpen);

        if (!overlayOpen)
            return;

        if (_label != null)
            _label.text = NguiRichText.FromOverlayText(OverlayTextFormatter.FormatMain(vm, live));

        SyncStatRows(live, Plugin.CanEditStats(live));
    }

    public static void Hide()
    {
        if (_dimGo != null)
            _dimGo.SetActive(false);
        if (_panelGo != null)
            _panelGo.SetActive(false);
        if (_toastGo != null)
            _toastGo.SetActive(false);
        if (_hudLabel != null)
            _hudLabel.gameObject.SetActive(false);
    }

    public static void ShowPickHint(string title, string body, string sub)
    {
        if (!EnsureBuilt() || _toastGo == null || _toastTitle == null || _toastBody == null || Plugin.OverlayOpen)
        {
            HidePickHint();
            return;
        }

        SetToastColors(new Color(0.1f, 0.38f, 0.2f, 0.94f));
        _toastGo.SetActive(true);
        _toastTitle.text = title;
        _toastBody.text = MonoUtil.HasText(sub) ? body + "\n" + sub : body;
    }

    public static void HidePickHint()
    {
        if (_toastGo != null && !SecretEndingAlerts.HasActiveToast)
            _toastGo.SetActive(false);
    }

    public static void ShowSecretToast(string title, string body)
    {
        if (!EnsureBuilt() || _toastGo == null || _toastTitle == null || _toastBody == null || Plugin.OverlayOpen)
            return;

        SetToastColors(new Color(0.5f, 0.1f, 0.42f, 0.96f));
        _toastGo.SetActive(true);
        _toastTitle.text = title;
        _toastBody.text = body;
    }

    public static void HideSecretToast()
    {
        if (_toastGo != null)
            _toastGo.SetActive(false);
    }

    private static void SetToastColors(Color bg)
    {
        if (_toastGo == null)
            return;

        NguiSolidBox.SetColor(_toastGo, bg);
    }

    private static bool EnsureBuilt()
    {
        if (_built && _rootGo != null)
            return true;

        var refLabel = FindReferenceLabel();
        if (refLabel == null)
            return false;

        var parent = FindUiParent(refLabel);
        if (parent == null)
            return false;

        return BuildWidgets(parent, refLabel);
    }

    private static bool BuildWidgets(Transform parent, UILabel refLabel)
    {
        try
        {
            _rootGo = new GameObject("MPHelper_OverlayRoot");
            _rootGo.transform.parent = parent;
            _rootGo.transform.localPosition = Vector3.zero;
            _rootGo.transform.localScale = Vector3.one;
            _rootGo.layer = refLabel.gameObject.layer;

            var uiRoot = NGUITools.FindInParents<UIRoot>(_rootGo.transform) ?? parent.GetComponent<UIRoot>();
            GetUiExtents(uiRoot, out var screenW, out var screenH);

            var panel = _rootGo.AddComponent<UIPanel>();
            panel.depth = OverlayDepth;

            _dimGo = NguiSolidBox.Create("MPHelper_Dim", _rootGo.transform, OverlayDepth, screenW, screenH, new Color(0f, 0f, 0f, 0.35f));
            NGUITools.AddWidgetCollider(_dimGo);
            _dimGo.SetActive(false);

            _panelGo = new GameObject("MPHelper_Panel");
            _panelGo.transform.parent = _rootGo.transform;
            _panelGo.transform.localPosition = Vector3.zero;
            _panelGo.transform.localScale = Vector3.one;
            _panelGo.layer = _rootGo.layer;

            NguiSolidBox.Create("MPHelper_PanelBorder", _panelGo.transform, OverlayDepth + 1, PanelWidth + 8, PanelHeight + 8, new Color(0.35f, 0.45f, 0.58f, 0.95f));
            NguiSolidBox.Create("MPHelper_BG", _panelGo.transform, OverlayDepth + 2, PanelWidth, PanelHeight, new Color(0.07f, 0.09f, 0.16f, 0.96f));

            var labelGo = new GameObject("MPHelper_Label");
            labelGo.transform.parent = _panelGo.transform;
            labelGo.transform.localScale = Vector3.one;
            labelGo.layer = _rootGo.layer;

            _label = labelGo.AddComponent<UILabel>();
            ApplyLabelFont(refLabel, _label, BodyFontSize);
            _label.depth = OverlayDepth + 10;
            _label.width = BodyWidth;
            _label.height = BodyHeight;
            _label.color = Color.white;
            _label.supportEncoding = true;
            _label.overflowMethod = UILabel.Overflow.ClampContent;
            _label.spacingY = 2;
            labelGo.transform.localPosition = new Vector3(0f, 95f, 0f);

            NguiSolidBox.Create(
                "MPHelper_StatsDivider",
                _panelGo.transform,
                OverlayDepth + 3,
                BodyWidth,
                2,
                new Color(0.35f, 0.45f, 0.58f, 0.55f))
                .transform.localPosition = new Vector3(0f, -35f, 0f);

            BuildStatRows(_panelGo.transform, refLabel);
            _panelGo.SetActive(false);

            _hudLabel = CreateAnchoredLabel("MPHelper_Hud", _rootGo.transform, refLabel, OverlayDepth + 20, HudFontSize);
            _hudLabel.transform.localPosition = new Vector3(screenW * 0.5f - 20f, screenH * 0.5f - 36f, 0f);
            _hudLabel.width = 340;
            _hudLabel.height = 28;
            _hudLabel.alignment = NGUIText.Alignment.Right;
            _hudLabel.text = "F8 = Overlay  |  F9 = Alt";
            _hudLabel.color = new Color(0.85f, 1f, 0.9f);

            _toastGo = new GameObject("MPHelper_Toast");
            _toastGo.transform.parent = _rootGo.transform;
            _toastGo.transform.localPosition = new Vector3(screenW * 0.5f - 170f, -screenH * 0.5f + 56f, 0f);
            _toastGo.transform.localScale = Vector3.one;
            _toastGo.layer = _rootGo.layer;
            NguiSolidBox.Create("MPHelper_ToastBg", _toastGo.transform, OverlayDepth + 20, 300, 72, new Color(0.1f, 0.38f, 0.2f, 0.94f));

            _toastTitle = CreateAnchoredLabel("MPHelper_ToastTitle", _toastGo.transform, refLabel, OverlayDepth + 21, SmallFontSize);
            _toastTitle.transform.localPosition = new Vector3(-130f, 18f, 0f);
            _toastTitle.width = 280;
            _toastTitle.height = 22;
            _toastTitle.color = new Color(0.85f, 1f, 0.9f);

            _toastBody = CreateAnchoredLabel("MPHelper_ToastBody", _toastGo.transform, refLabel, OverlayDepth + 21, SmallFontSize - 2);
            _toastBody.transform.localPosition = new Vector3(-130f, -6f, 0f);
            _toastBody.width = 280;
            _toastBody.height = 34;
            _toastBody.color = Color.white;

            _toastGo.SetActive(false);

            var rootPanel = _rootGo.GetComponent<UIPanel>();
            if (rootPanel != null)
                rootPanel.Refresh();

            _built = true;
            Plugin.Log.LogInfo("NGUI-Overlay bereit (sichtbar ueber Spiel-UI).");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError("NGUI Build: " + ex);
            DestroyWidgets();
            return false;
        }
    }

    private static void BuildStatRows(Transform parent, UILabel refLabel)
    {
        _statsGo = new GameObject("MPHelper_Stats");
        _statsGo.transform.parent = parent;
        _statsGo.transform.localPosition = new Vector3(0f, -PanelHeight * 0.5f + 150f, 0f);
        _statsGo.transform.localScale = Vector3.one;
        _statsGo.layer = parent.gameObject.layer;

        _statsTitle = CreateAnchoredLabel("MPHelper_StatsTitle", _statsGo.transform, refLabel, OverlayDepth + 12, SmallFontSize);
        _statsTitle.transform.localPosition = new Vector3(-200f, 8f, 0f);
        _statsTitle.width = 400;
        _statsTitle.height = 20;
        _statsTitle.text = "Stats (+/- zum Aendern):";
        _statsTitle.color = new Color(0.75f, 0.8f, 0.85f);

        for (var i = 0; i < EventDb.StatKeys.Length; i++)
        {
            var key = EventDb.StatKeys[i];
            var y = -18f - i * 28f;
            var row = new StatRowUi { Key = key };

            var val = CreateAnchoredLabel("MPHelper_Stat_" + key, _statsGo.transform, refLabel, OverlayDepth + 12, SmallFontSize);
            val.transform.localPosition = new Vector3(-20f, y, 0f);
            val.width = 44;
            val.height = 22;
            val.alignment = NGUIText.Alignment.Center;
            row.ValueLabel = val;
            StatRows[i] = row;

            var keyCopy = key;
            row.MinusBtn = CreateMiniStatButton(_statsGo.transform, refLabel, -178f, y, "-", () => Plugin.Instance?.NudgeStat(keyCopy, -1));
            row.PlusBtn = CreateMiniStatButton(_statsGo.transform, refLabel, 36f, y, "+", () => Plugin.Instance?.NudgeStat(keyCopy, 1));

            var nameLabel = CreateAnchoredLabel("MPHelper_StatName_" + key, _statsGo.transform, refLabel, OverlayDepth + 12, SmallFontSize);
            nameLabel.transform.localPosition = new Vector3(-200f, y, 0f);
            nameLabel.width = 110;
            nameLabel.height = 22;
            nameLabel.text = key;
        }
    }

    private static GameObject CreateMiniStatButton(
        Transform parent,
        UILabel refLabel,
        float x,
        float y,
        string caption,
        System.Action onClick)
    {
        var go = new GameObject("MPHelper_Btn_" + caption);
        go.transform.parent = parent;
        go.transform.localPosition = new Vector3(x, y, 0f);
        go.transform.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;

        NguiSolidBox.Add(go.transform, OverlayDepth + 13, 40, 26, new Color(0.18f, 0.32f, 0.5f, 1f));

        var lbl = CreateAnchoredLabel("T", go.transform, refLabel, OverlayDepth + 14, SmallFontSize + 1);
        lbl.transform.localPosition = Vector3.zero;
        lbl.width = 40;
        lbl.height = 26;
        lbl.text = caption;
        lbl.alignment = NGUIText.Alignment.Center;

        NGUITools.AddWidgetCollider(go);
        UIEventListener.Get(go).onClick = _ => onClick();
        return go;
    }

    private static void SyncStatRows(LiveGameState live, bool editable)
    {
        if (_statsGo == null)
            return;

        _statsGo.SetActive(true);

        if (_statsTitle != null)
        {
            _statsTitle.text = editable
                ? "Stats (+/- zum Aendern):"
                : "Stats (Bearbeiten in MainGame-Runde):";
        }

        for (var i = 0; i < StatRows.Length; i++)
        {
            SetStatButtonState(StatRows[i].MinusBtn, editable);
            SetStatButtonState(StatRows[i].PlusBtn, editable);

            if (StatRows[i].ValueLabel == null)
                continue;
            StatRows[i].ValueLabel.text = live.Stats.TryGetValue(StatRows[i].Key, out var v) ? v.ToString() : "0";
        }
    }

    private static void SetStatButtonState(GameObject? btn, bool editable)
    {
        if (btn == null)
            return;

        btn.SetActive(true);

        var tex = btn.GetComponent<UITexture>();
        if (tex != null)
        {
            tex.color = editable
                ? new Color(0.18f, 0.32f, 0.5f, 1f)
                : new Color(0.14f, 0.16f, 0.2f, 0.55f);
        }
    }

    private static bool CanEditStats(LiveGameState live) => Plugin.CanEditStats(live);

    private static UILabel CreateAnchoredLabel(string name, Transform parent, UILabel refLabel, int depth, int fontSize)
    {
        var go = new GameObject(name);
        go.transform.parent = parent;
        go.transform.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;

        var label = go.AddComponent<UILabel>();
        ApplyLabelFont(refLabel, label, fontSize);
        label.depth = depth;
        return label;
    }

    private static void GetUiExtents(UIRoot? uiRoot, out int width, out int height)
    {
        height = 720;
        width = 1280;
        if (uiRoot == null)
            return;

        height = uiRoot.activeHeight > 0 ? uiRoot.activeHeight : height;
        var aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        width = Mathf.RoundToInt(height * aspect);
    }

    private static Transform? FindUiParent(UILabel? refLabel)
    {
        if (refLabel != null)
        {
            var uiRoot = NGUITools.FindInParents<UIRoot>(refLabel.transform);
            if (uiRoot != null)
                return uiRoot.transform;

            var panel = NGUITools.FindInParents<UIPanel>(refLabel.transform);
            if (panel != null)
                return panel.transform;
        }

        var named = GameObject.Find("Title - UI Root") ?? GameObject.Find("UI Root");
        if (named != null)
            return named.transform;

        var cm = Object.FindObjectOfType<CameraManager>();
        if (cm != null && cm.cameraMiddleAndFront != null)
            return cm.cameraMiddleAndFront.transform;

        var cams = Object.FindObjectsOfType<UICamera>(true);
        if (cams != null && cams.Length > 0)
            return cams[0].transform;

        return null;
    }

    private static void ApplyLabelFont(UILabel src, UILabel dst, int fontSize)
    {
        dst.bitmapFont = src.bitmapFont;
        dst.ambigiousFont = src.ambigiousFont;
        try { dst.font = src.font; } catch { /* older NGUI */ }
        dst.fontSize = fontSize;
        dst.spacingY = 2;
    }

    private static UILabel? FindReferenceLabel()
    {
        var view = Object.FindObjectOfType<MainGameEventView>();
        if (view != null && view.simpleTextBox != null && view.simpleTextBox.label != null)
            return view.simpleTextBox.label;

        var btn = Object.FindObjectOfType<ButtonHandler>();
        if (btn != null && btn.label != null)
            return btn.label;

        var labels = FindAllUiLabels();
        if (labels == null || labels.Length == 0)
            return null;

        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            if (labels[i].bitmapFont != null || labels[i].ambigiousFont != null)
                return labels[i];
        }

        return labels[0];
    }

    private static UILabel[] FindAllUiLabels()
    {
        return Object.FindObjectsOfType<UILabel>(true);
    }

    private static void DestroyWidgets()
    {
        if (_rootGo != null)
            Object.Destroy(_rootGo);

        _rootGo = null;
        _dimGo = null;
        _panelGo = null;
        _label = null;
        _hudLabel = null;
        _toastGo = null;
        _toastTitle = null;
        _toastBody = null;
        _statsGo = null;
        _statsTitle = null;

        for (var i = 0; i < StatRows.Length; i++)
            StatRows[i] = new StatRowUi();

        _built = false;
    }
}
