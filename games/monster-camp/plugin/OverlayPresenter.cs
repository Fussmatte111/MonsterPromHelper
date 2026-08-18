using System.Collections.Generic;
using System.Text;

namespace MonsterCampHelper.Ingame;

public static class OverlayPresenter
{
    public static OverlayViewModel Build(LiveGameState live, EventDb db, string status)
    {
        var vm = new OverlayViewModel
        {
            Status = status,
            SceneName = live.SceneName,
            InSchool = live.InSchool,
            EventActive = live.EventActive,
            DbEventCount = db.EventCount,
            PlayerColor = live.PlayerColor,
            DialogText = live.DialogText,
            Option1Text = live.Option1Text,
            Option2Text = live.Option2Text,
            EnginePick = live.EnginePick,
            EngineStat = live.EngineStat,
            StatsLine = BuildStatsLine(live.Stats),
        };

        EventRecord? ev = null;
        if (MonoUtil.HasText(live.EventName))
        {
            ev = db.Get(live.EventName);
            if (ev != null)
            {
                vm.EventName = ev.Name;
                vm.MatchSource = "Game (EventName)";
            }
            else
                vm.EventName = live.EventName;
        }

        if (ev == null)
        {
            var hintMatch = db.MatchByTwoOptions(
                live.DialogText,
                live.Option1Text,
                live.Option2Text,
                relaxed: true);
            if (hintMatch.Best != null)
            {
                ev = hintMatch.Best;
                vm.EventName = ev.Name;
                vm.MatchSource = "Choice text";
                vm.MatchNote = hintMatch.Status;
            }
            else if (hintMatch.Hits.Count > 0)
            {
                vm.MatchNote = hintMatch.Status;
                foreach (var hit in hintMatch.Hits)
                    vm.HitLines.Add(hit.Name + " — " + hit.Route);
            }
            else if (MonoUtil.HasText(live.DialogText))
            {
                foreach (var hit in db.SearchDialog(live.DialogText, 6))
                    vm.HitLines.Add(hit.Name + " — " + hit.Route);
                if (vm.HitLines.Count > 0)
                    vm.MatchNote = "Dialog match — check the event in the log";
            }
        }

        if (ev != null)
        {
            vm.Route = ev.Route;
            vm.EventType = ev.Type;
            vm.SecretBanner = db.BuildSecretBanner(ev.Name, ev.Type);
            vm.OnSecretRoute = MonoUtil.HasText(vm.SecretBanner);

            var rec = db.Recommend(ev, live.Stats);
            vm.DbOptions = BuildOptionLines(rec);
            vm.RecommendedOption = rec.BestOption;
            vm.RecommendedStat = rec.BestStat;
            vm.RecommendedValue = rec.BestValue;
            vm.RecommendedHint = rec.BestHint;
            vm.RecommendedVerdict = rec.BestVerdict;
        }
        else if (!string.IsNullOrEmpty(vm.EventName))
        {
            vm.MatchNote = "Event not in events_db.json";
            vm.SecretBanner = db.BuildSecretBanner(vm.EventName, "");
            vm.OnSecretRoute = MonoUtil.HasText(vm.SecretBanner);
        }

        if (vm.DbEventCount == 0)
            vm.FooterLines.Add("0 events — missing data/events_db.json?");
        else
            vm.FooterLines.Add("DB: " + vm.DbEventCount + " Events  |  F8 to close");

        return vm;
    }

    private static string BuildStatsLine(IDictionary<string, int> stats)
    {
        if (stats.Count == 0)
            return "Stats: —";

        var sb = new StringBuilder();
        foreach (var key in EventDb.StatKeys)
        {
            if (stats.TryGetValue(key, out var v))
                sb.Append(key).Append(' ').Append(v).Append("  ");
        }
        return MonoUtil.TrimEndWhitespace(sb.ToString());
    }

    private static List<OptionDisplayLine> BuildOptionLines(Recommendation rec)
    {
        var lines = new List<OptionDisplayLine>();
        foreach (var line in rec.Lines)
        {
            lines.Add(new OptionDisplayLine
            {
                Option = line.Option,
                Stat = line.Stat,
                Value = line.Value,
                Hint = line.Hint,
                Verdict = line.Verdict,
                IsRecommended = line.Option == rec.BestOption && rec.BestOption > 0,
            });
        }
        return lines;
    }
}
