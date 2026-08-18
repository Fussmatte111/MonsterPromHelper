using UnityEngine;

namespace MonsterCampHelper.Ingame;

/// <summary>Small persistent hint during dialogs: recommended option without opening F8.</summary>
public static class PickHintToast
{
    private static bool _visible;
    private static string _title = "";
    private static string _body = "";
    private static string _sub = "";
    private static string _lastFingerprint = "";

    public static void ResetScene()
    {
        _visible = false;
        _lastFingerprint = "";
    }

    public static void Tick(EventDb db, GameBridge bridge, bool enabled)
    {
        if (!enabled || db == null)
        {
            _visible = false;
            return;
        }

        if (Plugin.OverlayOpen)
        {
            _visible = false;
            return;
        }

        LiveGameState live;
        string err;
        if (!bridge.TryRead(out live, out err) || !live.InSchool)
        {
            _visible = false;
            return;
        }

        if (!GameBridge.IsSchoolEventDialog(live))
        {
            _visible = false;
            return;
        }

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 4 && o2.Length < 4)
        {
            _visible = false;
            return;
        }

        var fp = o1.ToLowerInvariant() + "||" + o2.ToLowerInvariant();
        if (fp == _lastFingerprint)
            return;

        _lastFingerprint = fp;
        RebuildHint(live, db);
    }

    private static void RebuildHint(LiveGameState live, EventDb db)
    {
        _visible = false;
        _title = "";
        _body = "";
        _sub = "";

        try
        {
            var vm = OverlayPresenter.Build(live, db, "");

            if (vm.RecommendedOption > 0 && vm.RecommendedVerdict == "success")
            {
                _visible = true;
                _title = "RECOMMENDED: option " + vm.RecommendedOption;
                _body = vm.RecommendedStat + " " + vm.RecommendedValue;
                if (MonoUtil.HasText(vm.RecommendedHint))
                    _sub = Truncate(vm.RecommendedHint, 42);
            }
            else if (live.EnginePick > 0)
            {
                _visible = true;
                _title = "Game picks: option " + live.EnginePick;
                _body = live.EngineStat;
                if (vm.RecommendedOption > 0 && vm.RecommendedOption != live.EnginePick)
                    _sub = "DB: Option " + vm.RecommendedOption + " (" + vm.RecommendedStat + ")";
            }
            else if (vm.RecommendedOption > 0)
            {
                _visible = true;
                _title = "Lean: option " + vm.RecommendedOption;
                _body = vm.RecommendedStat + " " + vm.RecommendedValue;
                _sub = "No clear advantage";
            }
            else if (live.EnginePick == 0 && MonoUtil.HasText(live.StatOption1) && MonoUtil.HasText(live.StatOption2)
                && live.Stats.Count > 0)
            {
                _visible = true;
                _title = "Stats tied";
                _body = live.StatOption1 + " vs " + live.StatOption2;
                _sub = "F8 for details";
            }

            if (_visible && MonoUtil.HasText(vm.EventName))
            {
                var evShort = vm.EventName.Length > 28 ? vm.EventName.Substring(0, 25) + "..." : vm.EventName;
                if (MonoUtil.HasText(_sub))
                    _sub = evShort + "  |  " + _sub;
                else
                    _sub = evShort;
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogWarning("Pick hint: " + ex.Message);
            _visible = false;
        }
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 3) + "...";
    }

    public static void Draw()
    {
        if (!_visible)
            return;

        OverlayGui.DrawPickHint(_title, _body, _sub);
    }
}
