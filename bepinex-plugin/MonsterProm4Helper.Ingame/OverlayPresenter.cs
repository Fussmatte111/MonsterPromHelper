using System.Collections.Generic;
using System.Text;

namespace MonsterProm4Helper.Ingame;

public static class OverlayPresenter
{
    public static OverlayViewModel Build(LiveGameState live, EventDb db, string status)
    {
        var vm = new OverlayViewModel
        {
            Status = status,
            SceneName = live.SceneName,
            InSchool = live.InSchool,
            InPregame = live.InPregame,
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

        ApplyLiveExchange(live, vm);

        EventRecord ev = null;
        if (MonoUtil.HasText(live.EventName))
        {
            ev = db.Get(live.EventName);
            if (ev != null)
            {
                vm.EventName = ev.Name;
                vm.MatchSource = "Spiel (EventName)";
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
                vm.MatchSource = "Antwort-Text";
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
                    vm.MatchNote = "Dialog-Treffer — Event im Log prüfen";
            }
        }

        if (ev != null)
        {
            vm.Route = ev.Route;
            vm.EventType = ev.Type;
            vm.SecretBanner = db.BuildSecretBanner(ev.Name, ev.Type);
            vm.OnSecretRoute = MonoUtil.HasText(vm.SecretBanner);

            var rec = db.Recommend(ev, live.Stats);
            MergeDbRecommendations(vm, rec);
        }
        else if (!string.IsNullOrEmpty(vm.EventName))
        {
            if (vm.DbOptions.Count == 0)
                vm.MatchNote = "Live-Daten (Stat-Austausch) — keine DB";
            vm.SecretBanner = db.BuildSecretBanner(vm.EventName, "");
            vm.OnSecretRoute = MonoUtil.HasText(vm.SecretBanner);
        }

        if (vm.DbEventCount == 0)
            vm.FooterLines.Add("Live Stat-Austausch aktiv  |  F8 schliessen");
        else
            vm.FooterLines.Add("DB: " + vm.DbEventCount + " Events  |  Live +/−  |  F8 schliessen");

        if (live.InPregame)
            vm.PregameLines.AddRange(db.BuildPregameSummary(live));

        return vm;
    }

    private static void ApplyLiveExchange(LiveGameState live, OverlayViewModel vm)
    {
        if (live.ExchangeOptions.Count == 0)
            return;

        var bestOption = 0;
        var bestScore = int.MinValue;

        for (var i = 0; i < live.ExchangeOptions.Count; i++)
        {
            var ex = live.ExchangeOptions[i];
            var gainVal = live.Stats.TryGetValue(ex.GainStat, out var gv) ? gv : 0;
            var loseVal = live.Stats.TryGetValue(ex.LoseStat, out var lv) ? lv : 0;
            var score = loseVal - gainVal;

            if (ex.IsSuccess)
                score += 1000;

            var line = new OptionDisplayLine
            {
                Option = ex.Option,
                Stat = "+" + ex.GainStat + " / -" + ex.LoseStat,
                Value = gainVal,
                Hint = "du: " + ex.GainStat + "=" + gainVal + ", " + ex.LoseStat + "=" + loseVal,
                Verdict = ex.IsSuccess ? "success" : "exchange",
                IsRecommended = false,
            };
            vm.DbOptions.Add(line);

            if (score > bestScore)
            {
                bestScore = score;
                bestOption = ex.Option;
            }
        }

        if (bestOption <= 0)
            return;

        vm.RecommendedOption = bestOption;
        for (var i = 0; i < vm.DbOptions.Count; i++)
        {
            if (vm.DbOptions[i].Option == bestOption)
            {
                vm.DbOptions[i].IsRecommended = true;
                vm.RecommendedStat = vm.DbOptions[i].Stat;
                vm.RecommendedValue = vm.DbOptions[i].Value;
                vm.RecommendedHint = vm.DbOptions[i].Hint;
                vm.RecommendedVerdict = vm.DbOptions[i].Verdict;
                break;
            }
        }
    }

    private static void MergeDbRecommendations(OverlayViewModel vm, Recommendation rec)
    {
        if (vm.RecommendedOption <= 0 && rec.BestOption > 0)
        {
            vm.RecommendedOption = rec.BestOption;
            vm.RecommendedStat = rec.BestStat;
            vm.RecommendedValue = rec.BestValue;
            vm.RecommendedHint = rec.BestHint;
            vm.RecommendedVerdict = rec.BestVerdict;
        }

        if (vm.DbOptions.Count > 0)
            return;

        foreach (var line in rec.Lines)
        {
            vm.DbOptions.Add(new OptionDisplayLine
            {
                Option = line.Option,
                Stat = line.Stat,
                Value = line.Value,
                Hint = line.Hint,
                Verdict = line.Verdict,
                IsRecommended = line.Option == rec.BestOption && rec.BestOption > 0,
            });
        }
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
}
