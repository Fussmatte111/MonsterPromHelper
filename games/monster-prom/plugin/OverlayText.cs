using System.Text;

namespace MonsterPromHelper.Ingame;

/// <summary>Plain-text fallback for NGUI overlay.</summary>
public static class OverlayText
{
    public static string Build(OverlayViewModel vm)
    {
        var sb = new StringBuilder(400);
        sb.AppendLine("Monster Prom Helper");
        sb.AppendLine("F8 = close");
        sb.AppendLine();

        if (vm.OnSecretRoute && MonoUtil.HasText(vm.SecretBanner))
            sb.AppendLine(vm.SecretBanner);

        if (vm.RecommendedOption > 0)
            sb.AppendLine("RECOMMENDED: option " + vm.RecommendedOption + " (" + vm.RecommendedStat + " " + vm.RecommendedValue + ")");

        sb.AppendLine(vm.Status);
        if (MonoUtil.HasText(vm.EventName))
            sb.AppendLine("Event: " + vm.EventName);
        if (MonoUtil.HasText(vm.Route))
            sb.AppendLine(vm.Route);
        sb.AppendLine(vm.StatsLine ?? "");

        if (MonoUtil.HasText(vm.Option1Text))
            sb.AppendLine("1) " + vm.Option1Text);
        if (MonoUtil.HasText(vm.Option2Text))
            sb.AppendLine("2) " + vm.Option2Text);

        for (var i = 0; i < vm.DbOptions.Count; i++)
        {
            var o = vm.DbOptions[i];
            var mark = o.IsRecommended ? ">>" : "  ";
            sb.AppendLine(mark + " Opt " + o.Option + ": " + o.Stat + "=" + o.Value);
        }

        for (var i = 0; i < vm.FooterLines.Count; i++)
            sb.AppendLine(vm.FooterLines[i]);

        return sb.ToString();
    }
}
