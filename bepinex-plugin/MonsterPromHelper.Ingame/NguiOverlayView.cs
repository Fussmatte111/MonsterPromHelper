using System.Reflection;
using UnityEngine;

namespace MonsterPromHelper.Ingame;

/// <summary>Legacy NGUI text layer (disabled — F8 uses IMGUI panel only).</summary>
public static class NguiOverlayView
{
    private const int OverlayDepth = 32000;

    private static GameObject? _rootGo;
    private static UILabel? _label;
    private static bool _built;

    public static bool IsActive => _rootGo != null && _rootGo.activeInHierarchy;

    public static void ResetForNewScene()
    {
        DestroyWidgets();
    }

    public static bool TryShow(string text)
    {
        try
        {
            var em = EventManager.Instance;
            if (em == null)
            {
                Plugin.Log.LogWarning("NGUI: EventManager fehlt — nur IMGUI.");
                return false;
            }

            if (em.EventTextLabel == null)
            {
                Plugin.Log.LogWarning("NGUI: EventTextLabel fehlt — nur IMGUI.");
                return false;
            }

            var parent = FindUiParent();
            if (parent == null)
            {
                Plugin.Log.LogWarning("NGUI: UICamera fehlt — nur IMGUI.");
                return false;
            }

            if (!_built && !BuildWidgets(em, parent))
                return false;

            if (_built)
                Plugin.Log.LogInfo("NGUI-Overlay aktiv.");

            SetText(text);
            _rootGo!.SetActive(true);
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"NGUI TryShow: {ex}");
            DestroyWidgets();
            return false;
        }
    }

    public static void Hide()
    {
        if (_rootGo != null)
            _rootGo.SetActive(false);
    }

    public static void Refresh(string text)
    {
        if (_built)
            SetText(text);
    }

    private static bool BuildWidgets(EventManager em, Transform parent)
    {
        try
        {
            _rootGo = new GameObject("MPHelper_OverlayRoot");
            _rootGo.transform.parent = parent;
            _rootGo.transform.localPosition = Vector3.zero;
            _rootGo.transform.localScale = Vector3.one;
            _rootGo.layer = em.EventTextLabel.gameObject.layer;

            var panel = _rootGo.AddComponent<UIPanel>();
            panel.depth = OverlayDepth;

            if (em.UISprite_Frame != null)
            {
                var bgGo = Object.Instantiate(em.UISprite_Frame.gameObject);
                bgGo.name = "MPHelper_BG";
                bgGo.transform.parent = _rootGo.transform;
                bgGo.transform.localPosition = Vector3.zero;
                bgGo.transform.localScale = Vector3.one;
                bgGo.layer = _rootGo.layer;
                SetWidgetDepth(bgGo, OverlayDepth);
                SetWidgetSize(bgGo, 920, 680);
                SetWidgetColor(bgGo, new Color(0.08f, 0.1f, 0.18f, 0.95f));
            }

            var labelGo = new GameObject("MPHelper_Label");
            labelGo.transform.parent = _rootGo.transform;
            labelGo.transform.localPosition = new Vector3(0f, 20f, 0f);
            labelGo.transform.localScale = Vector3.one;
            labelGo.layer = _rootGo.layer;

            _label = labelGo.AddComponent<UILabel>();
            CopyLabelSettings(em.EventTextLabel, _label);
            _label.depth = OverlayDepth + 10;
            _label.width = 500;
            _label.height = 620;
            _label.color = Color.white;
            _label.overflowMethod = UILabel.Overflow.ClampContent;

            _rootGo.SetActive(false);
            _built = true;
            Plugin.Log.LogInfo("NGUI-Widgets erstellt.");
            return true;
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"NGUI Build: {ex}");
            DestroyWidgets();
            return false;
        }
    }

    private static Transform? FindUiParent()
    {
        var uiType = MonoUtil.FindGameType("UICamera");
        if (!MonoUtil.IsNull(uiType))
        {
            var cams = Object.FindObjectsOfType(uiType);
            if (cams != null && cams.Length > 0)
            {
                var cam = cams[0] as Component;
                if (!MonoUtil.IsNull(cam))
                    return cam.transform;
            }
        }

        var uiRoot = GameObject.Find("UI Root");
        if (!MonoUtil.IsNull(uiRoot))
            return uiRoot.transform;

        return null;
    }

    private static void CopyLabelSettings(UILabel src, UILabel dst)
    {
        dst.bitmapFont = src.bitmapFont;
        dst.ambigiousFont = src.ambigiousFont;
        dst.fontSize = src.fontSize > 0 ? src.fontSize : 18;
        CopyField(src, dst, "mFont");
        CopyField(src, dst, "mTrueTypeFont");
        CopyField(src, dst, "mFontSize");
    }

    private static void SetWidgetDepth(GameObject go, int depth)
    {
        var w = go.GetComponent<UIWidget>();
        if (!MonoUtil.IsNull(w))
            w.depth = depth;
    }

    private static void SetWidgetSize(GameObject go, int w, int h)
    {
        var widget = go.GetComponent<UIWidget>();
        if (!MonoUtil.IsNull(widget))
        {
            widget.width = w;
            widget.height = h;
        }
    }

    private static void SetWidgetColor(GameObject go, Color c)
    {
        var widget = go.GetComponent<UIWidget>();
        if (!MonoUtil.IsNull(widget))
            widget.color = c;
    }

    private static void CopyField(object src, object dst, string name)
    {
        var t = src.GetType();
        var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (MonoUtil.IsNull(f))
            return;
        f.SetValue(dst, f.GetValue(src));
    }

    private static void SetText(string text)
    {
        if (_label != null)
            _label.text = text ?? "";
    }

    private static void DestroyWidgets()
    {
        if (_rootGo != null)
            Object.Destroy(_rootGo);
        _rootGo = null;
        _label = null;
        _built = false;
    }
}
