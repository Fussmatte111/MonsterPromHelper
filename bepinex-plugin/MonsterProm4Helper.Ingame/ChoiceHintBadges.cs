using BeautifulGlitch;
using Game;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterProm4Helper.Ingame;

/// <summary>Stat / LI badges on the left of choice stat boxes during dialogs and pregame picks.</summary>
public static class ChoiceHintBadges
{
    private const int BadgeDepth = 31000;
    private const float LeftOffset = -58f;

    private static GameObject? _rootGo;
    private static UILabel? _refLabel;
    private static readonly List<BadgeRow> Rows = new List<BadgeRow>();
    private static bool _built;

    private sealed class BadgeRow
    {
        public GameObject Go = null!;
        public UILabel StatLabel = null!;
        public UILabel RoLabel = null!;
    }

    private sealed class AnchorInfo
    {
        public Transform Transform = null!;
        public string Caption = "";
    }

    public static void ResetScene()
    {
        if (_rootGo != null)
            UnityEngine.Object.Destroy(_rootGo);
        _rootGo = null;
        _refLabel = null;
        Rows.Clear();
        _built = false;
    }

    public static void Sync(GameBridge bridge, EventDb db)
    {
        if (Plugin.OverlayOpen || db == null)
        {
            HideAll();
            return;
        }

        LiveGameState live;
        string err;
        if (!bridge.TryRead(out live, out err) || (!live.InSchool && !live.InPregame))
        {
            HideAll();
            return;
        }

        if (!DialogContext.IsChoiceDecisionActive(live))
        {
            HideAll();
            return;
        }

        if (!EnsureBuilt())
            return;

        var anchors = CollectAnchors(live);
        if (anchors.Count == 0)
        {
            HideAll();
            return;
        }

        EnsureRows(anchors.Count);

        for (var i = 0; i < anchors.Count; i++)
        {
            var info = ResolveBadgeInfo(live, db, i, anchors[i].Caption);
            PlaceRow(Rows[i], anchors[i].Transform, info);
            Rows[i].Go.SetActive(true);
        }

        for (var i = anchors.Count; i < Rows.Count; i++)
            Rows[i].Go.SetActive(false);
    }

    private static bool EnsureBuilt()
    {
        if (_built && _rootGo != null)
            return true;

        _refLabel = NguiOverlayView.FindReferenceLabelPublic();
        if (_refLabel == null)
            return false;

        var parent = FindUiParent(_refLabel);
        if (parent == null)
            return false;

        _rootGo = new GameObject("MPHelper_ChoiceBadges");
        _rootGo.transform.parent = parent;
        _rootGo.transform.localPosition = Vector3.zero;
        _rootGo.transform.localScale = Vector3.one;
        _rootGo.layer = _refLabel.gameObject.layer;

        var panel = _rootGo.AddComponent<UIPanel>();
        panel.depth = BadgeDepth;

        _built = true;
        return true;
    }

    private static void EnsureRows(int count)
    {
        while (Rows.Count < count)
        {
            var rowGo = new GameObject("MPHelper_Badge_" + Rows.Count);
            rowGo.transform.parent = _rootGo!.transform;
            rowGo.transform.localScale = Vector3.one;
            rowGo.layer = _refLabelLayer();

            NguiSolidBox.Add(rowGo.transform, BadgeDepth + 1, 52, 46, new Color(0.04f, 0.06f, 0.1f, 0.88f));

            var stat = CreateBadgeLabel(rowGo.transform, BadgeDepth + 2, 18);
            stat.transform.localPosition = new Vector3(0f, 8f, 0f);
            stat.width = 50;
            stat.height = 20;
            stat.alignment = NGUIText.Alignment.Center;

            var ro = CreateBadgeLabel(rowGo.transform, BadgeDepth + 2, 14);
            ro.transform.localPosition = new Vector3(0f, -10f, 0f);
            ro.width = 50;
            ro.height = 18;
            ro.alignment = NGUIText.Alignment.Center;
            ro.color = new Color(0.85f, 0.9f, 1f);

            Rows.Add(new BadgeRow { Go = rowGo, StatLabel = stat, RoLabel = ro });
        }
    }

    private static int _refLabelLayer()
    {
        return _refLabel != null ? _refLabel.gameObject.layer : 0;
    }

    private static UILabel CreateBadgeLabel(Transform parent, int depth, int fontSize)
    {
        var go = new GameObject("Lbl");
        go.transform.parent = parent;
        go.transform.localScale = Vector3.one;
        go.layer = parent.gameObject.layer;

        var label = go.AddComponent<UILabel>();
        NguiOverlayView.ApplyLabelFontPublic(_refLabel!, label, fontSize);
        label.depth = depth;
        return label;
    }

    private static void PlaceRow(BadgeRow row, Transform anchor, BadgeInfo info)
    {
        row.Go.transform.parent = anchor.parent;
        row.Go.transform.localPosition = anchor.localPosition + new Vector3(LeftOffset, 0f, 0f);
        row.Go.transform.localScale = Vector3.one;
        row.StatLabel.text = info.StatLine;
        row.StatLabel.color = StatColor(info.GainStat);
        row.RoLabel.text = info.RoLine;
        row.RoLabel.gameObject.SetActive(MonoUtil.HasText(info.RoLine));
    }

    private static BadgeInfo ResolveBadgeInfo(LiveGameState live, EventDb db, int index, string caption)
    {
        if (index < live.ExchangeOptions.Count)
        {
            var ex = live.ExchangeOptions[index];
            return new BadgeInfo
            {
                GainStat = ex.GainStat,
                LoseStat = ex.LoseStat,
                StatLine = "+" + StatShort(ex.GainStat) + " / -" + StatShort(ex.LoseStat),
            };
        }

        if (live.InPregame)
        {
            var like = db.LookupPregameLike(caption);
            if (like != null)
            {
                return new BadgeInfo
                {
                    GainStat = like.Stat,
                    StatLine = "+" + StatShort(like.Stat),
                    RoLine = FormatCharacters(like.Characters),
                };
            }

            var itemStat = db.LookupPregameItemStat(caption);
            if (MonoUtil.HasText(itemStat))
            {
                return new BadgeInfo
                {
                    GainStat = itemStat,
                    StatLine = "+" + StatShort(itemStat),
                };
            }
        }

        return new BadgeInfo { StatLine = "?" };
    }

    private static List<AnchorInfo> CollectAnchors(LiveGameState live)
    {
        var list = new List<AnchorInfo>();

        AppendAnchors(list, FindBehaviours("Game.StatsBoxWidget"), mb => ReadNearbyCaption(mb.transform));
        if (list.Count == 0)
            AppendAnchors(list, FindBehaviours("Game.PrologueLikeButton"), ReadLikeCaption);
        if (list.Count == 0)
        {
            var buttons = UnityEngine.Object.FindObjectsOfType<ButtonHandler>(true);
            for (var i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null || !buttons[i].gameObject.activeInHierarchy)
                    continue;
                var text = buttons[i].label != null ? buttons[i].label.text : "";
                if ((text ?? "").Trim().Length < 4)
                    continue;
                list.Add(new AnchorInfo { Transform = buttons[i].transform, Caption = text });
            }
        }

        list.Sort((a, b) => b.Transform.localPosition.y.CompareTo(a.Transform.localPosition.y));

        if (live.ExchangeOptions.Count > 0 && list.Count > live.ExchangeOptions.Count)
            list.RemoveRange(live.ExchangeOptions.Count, list.Count - live.ExchangeOptions.Count);

        return list;
    }

    private delegate string CaptionReader(MonoBehaviour behaviour);

    private static void AppendAnchors(List<AnchorInfo> list, MonoBehaviour[] items, CaptionReader readCaption)
    {
        for (var i = 0; i < items.Length; i++)
        {
            var mb = items[i];
            if (mb == null || !mb.gameObject.activeInHierarchy)
                continue;
            list.Add(new AnchorInfo
            {
                Transform = mb.transform,
                Caption = readCaption(mb),
            });
        }
    }

    private static MonoBehaviour[] FindBehaviours(string typeName)
    {
        var type = FindType(typeName);
        if (type == null)
            return Array.Empty<MonoBehaviour>();

        var objects = UnityEngine.Object.FindObjectsOfType(type, true);
        if (objects == null || objects.Length == 0)
            return Array.Empty<MonoBehaviour>();

        var list = new List<MonoBehaviour>(objects.Length);
        for (var i = 0; i < objects.Length; i++)
        {
            if (objects[i] is MonoBehaviour mb)
                list.Add(mb);
        }

        return list.ToArray();
    }

    private static string ReadLikeCaption(MonoBehaviour btn)
    {
        var label = btn.GetComponentInChildren<UILabel>(true);
        if (label != null && MonoUtil.HasText(label.text))
            return label.text.Trim();

        var data = GameBridge.ReadMemberPublic(btn, "likeData")
            ?? GameBridge.ReadMemberPublic(btn, "mLikeData")
            ?? GameBridge.ReadMemberPublic(btn, "data");
        if (data != null)
        {
            var name = GameBridge.ReadMemberPublic(data, "nameId") as string
                ?? GameBridge.ReadMemberPublic(data, "name") as string
                ?? GameBridge.ReadMemberPublic(data, "likeName") as string;
            if (MonoUtil.HasText(name))
                return name!;
        }

        return btn.name;
    }

    private static string ReadNearbyCaption(Transform t)
    {
        var labels = t.GetComponentsInParent<UILabel>(true);
        for (var i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null || !labels[i].gameObject.activeInHierarchy)
                continue;
            var text = (labels[i].text ?? "").Trim();
            if (text.Length >= 8)
                return text;
        }

        var siblingButtons = t.parent != null
            ? t.parent.GetComponentsInChildren<ButtonHandler>(true)
            : Array.Empty<ButtonHandler>();
        for (var i = 0; i < siblingButtons.Length; i++)
        {
            if (siblingButtons[i]?.label == null)
                continue;
            var text = (siblingButtons[i].label.text ?? "").Trim();
            if (text.Length >= 8)
                return text;
        }

        return "";
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(fullName, false);
            if (t != null)
                return t;
        }

        return null;
    }

    private static Transform? FindUiParent(UILabel refLabel)
    {
        var uiRoot = NGUITools.FindInParents<UIRoot>(refLabel.transform);
        if (uiRoot != null)
            return uiRoot.transform;

        var panel = NGUITools.FindInParents<UIPanel>(refLabel.transform);
        return panel != null ? panel.transform : refLabel.transform.parent;
    }

    private static string StatShort(string stat)
    {
        switch ((stat ?? "").ToUpperInvariant())
        {
            case "SMARTS": return "S";
            case "BOLD": return "B";
            case "CREATIVE": return "C";
            case "CHARM": return "H";
            case "FUN": return "F";
            default: return stat ?? "?";
        }
    }

    private static string FormatCharacters(List<string> chars)
    {
        if (chars == null || chars.Count == 0)
            return "";

        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < chars.Count && i < 3; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(RoShort(chars[i]));
        }

        if (chars.Count > 3)
            sb.Append('+');
        return sb.ToString();
    }

    private static string RoShort(string name)
    {
        if (!MonoUtil.HasText(name))
            return "";
        if (name.Length <= 2)
            return name;
        return name.Substring(0, 2);
    }

    private static Color StatColor(string stat)
    {
        switch ((stat ?? "").ToUpperInvariant())
        {
            case "SMARTS": return new Color(0.45f, 0.85f, 1f);
            case "BOLD": return new Color(1f, 0.45f, 0.4f);
            case "CREATIVE": return new Color(0.85f, 0.55f, 1f);
            case "CHARM": return new Color(1f, 0.55f, 0.8f);
            case "FUN": return new Color(1f, 0.92f, 0.35f);
            default: return Color.white;
        }
    }

    private static void HideAll()
    {
        for (var i = 0; i < Rows.Count; i++)
            Rows[i].Go.SetActive(false);
    }

    private sealed class BadgeInfo
    {
        public string GainStat = "";
        public string LoseStat = "";
        public string StatLine = "";
        public string RoLine = "";
    }
}
