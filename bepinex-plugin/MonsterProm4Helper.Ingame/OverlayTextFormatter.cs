using System.Text;

namespace MonsterProm4Helper.Ingame;

public static class OverlayTextFormatter
{
    public static string FormatMain(OverlayViewModel vm, LiveGameState live)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>Monster Prom 4 Helper</b>");
        sb.AppendLine();

        if (vm.OnSecretRoute && MonoUtil.HasText(vm.SecretBanner))
        {
            sb.AppendLine("<color=#FF99EE><b>").Append(vm.SecretBanner).AppendLine("</b></color>");
            sb.AppendLine();
        }

        if (vm.RecommendedOption > 0)
        {
            sb.Append("<color=#AAFFAA><b>EMPFOHLEN: Option ").Append(vm.RecommendedOption);
            if (MonoUtil.HasText(vm.RecommendedStat))
                sb.Append(" (").Append(vm.RecommendedStat).Append(")");
            sb.AppendLine("</b></color>");
            if (MonoUtil.HasText(vm.RecommendedHint))
                sb.AppendLine(vm.RecommendedHint);
            sb.AppendLine();
        }

        if (vm.EnginePick > 0)
            sb.AppendLine("Spiel waehlt: Option " + vm.EnginePick + " (" + vm.EngineStat + ")");

        sb.AppendLine(vm.Status);
        sb.AppendLine();

        if (MonoUtil.HasText(vm.EventName))
        {
            sb.AppendLine(vm.EventName + (MonoUtil.HasText(vm.Route) ? "  |  " + vm.Route : ""));
        }

        if (MonoUtil.HasText(vm.StatsLine))
            sb.AppendLine(vm.StatsLine);

        if (MonoUtil.HasText(vm.DialogText))
        {
            sb.AppendLine();
            sb.AppendLine("<b>Dialog</b>");
            sb.AppendLine(Trim(vm.DialogText, 400));
        }

        AppendOption(sb, 1, vm.Option1Text, vm.RecommendedOption == 1, vm.DbOptions);
        AppendOption(sb, 2, vm.Option2Text, vm.RecommendedOption == 2, vm.DbOptions);

        for (var i = 0; i < vm.DbOptions.Count; i++)
        {
            var opt = vm.DbOptions[i];
            if (opt.Option == 1 || opt.Option == 2)
                continue;
            sb.AppendLine("Option " + opt.Option + ": " + opt.Stat);
        }

        if (vm.HitLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Moegliche Events:");
            for (var i = 0; i < vm.HitLines.Count && i < 4; i++)
                sb.AppendLine("  " + vm.HitLines[i]);
        }

        if (MonoUtil.HasText(vm.MatchNote))
            sb.AppendLine(vm.MatchNote);

        if (vm.FooterLines.Count > 0)
            sb.AppendLine(vm.FooterLines[0]);

        if (vm.PregameLines.Count > 0)
        {
            sb.AppendLine();
            for (var i = 0; i < vm.PregameLines.Count; i++)
                sb.AppendLine(vm.PregameLines[i]);
        }

        AppendLove(sb, live);
        return sb.ToString();
    }

    private static void AppendOption(StringBuilder sb, int num, string text, bool pick, System.Collections.Generic.List<OptionDisplayLine> opts)
    {
        if (!MonoUtil.HasText(text))
            return;

        sb.AppendLine();
        sb.Append(pick ? "<color=#AAFFAA><b>>> " : "");
        sb.Append("Option ").Append(num).Append(": ").Append(Trim(text, 120));
        if (pick)
            sb.Append(" <<</b></color>");
        else
            sb.AppendLine();

        for (var i = 0; i < opts.Count; i++)
        {
            if (opts[i].Option != num)
                continue;
            sb.Append("  ").Append(opts[i].Stat);
            if (MonoUtil.HasText(opts[i].Hint))
                sb.Append("  |  ").Append(Trim(opts[i].Hint, 50));
            sb.AppendLine();
        }
    }

    private static void AppendLove(StringBuilder sb, LiveGameState live)
    {
        sb.AppendLine();
        sb.AppendLine("<b>—— Zuneigung ——</b>");
        sb.AppendLine("LI * = ").Append(MonoUtil.HasText(live.LockedInterest) ? live.LockedInterest : "—");
        sb.AppendLine("Gut: ca. " + live.LoveThreshold + "+ Love");
        for (var i = 0; i < GameBridge.RomanceNpcs.Length; i++)
        {
            var npc = GameBridge.RomanceNpcs[i];
            var love = live.LovePoints.TryGetValue(npc, out var lv) ? lv : 0;
            var dates = live.InterestPoints.TryGetValue(npc, out var d) ? d : 0;
            var star = MonoUtil.HasText(live.LockedInterest)
                && string.Equals(live.LockedInterest, npc, System.StringComparison.OrdinalIgnoreCase) ? " *" : "";
            sb.AppendLine(npc + star + ": Love " + love + "  |  Dates " + dates);
        }
    }

    private static string Trim(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 3) + "...";
    }
}
