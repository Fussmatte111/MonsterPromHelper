namespace MonsterCampHelper.Ingame;

/// <summary>Filters story event dialogs from drinks roulette / prom choose / intros.</summary>
public static class DialogContext
{
    public static void ReadGamePhase(object tm, object player, LiveGameState state)
    {
        state.TurnTypeName = "";
        state.InCafeteriaPhase = false;
        state.CurrentLocationName = "";

        if (tm != null)
        {
            var turnMethod = tm.GetType().GetMethod(
                "GetCurrentTurnType",
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
            if (turnMethod != null)
                state.TurnTypeName = turnMethod.Invoke(tm, null)?.ToString() ?? "";
        }

        if (player != null)
        {
            var loc = GameBridge.ReadMemberPublic(player, "currentLocation");
            state.CurrentLocationName = loc?.ToString() ?? "";
        }

        if (IsNonEventTurn(state.TurnTypeName))
            state.InCafeteriaPhase = true;
    }

    public static bool IsMainGameEventDialog(LiveGameState live) => IsSchoolEventDialog(live);

    public static bool IsSchoolEventDialog(LiveGameState live)
    {
        if (!live.InSchool || live.InCafeteriaPhase)
            return false;

        if (!live.EventActive && !MonoUtil.HasText(live.StatOption1))
            return false;

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 4 || o2.Length < 4)
            return false;

        if (LooksLikeNavigationPick(o1, o2, live.DialogText))
            return false;

        if (!MonoUtil.HasText(live.StatOption1) || !MonoUtil.HasText(live.StatOption2))
            return false;

        return (live.DialogText ?? "").Trim().Length >= 8;
    }

    public static bool IsChoiceDecisionActive(LiveGameState live)
    {
        if (live.InCafeteriaPhase)
            return false;
        if (!live.InSchool && !live.InPregame)
            return false;

        if (MonoUtil.HasText(live.StatOption1) && MonoUtil.HasText(live.StatOption2))
            return HasReadableOptions(live);

        if (live.InSchool && IsSchoolEventDialog(live))
            return true;

        return live.EventActive && live.ChoiceCount >= 2;
    }

    private static bool HasReadableOptions(LiveGameState live)
    {
        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        return o1.Length >= 3 && o2.Length >= 3;
    }

    private static bool IsNonEventTurn(string turnType)
    {
        if (string.IsNullOrEmpty(turnType))
            return false;

        return turnType.IndexOf("Drink", System.StringComparison.OrdinalIgnoreCase) >= 0
            || turnType.IndexOf("PromChoose", System.StringComparison.OrdinalIgnoreCase) >= 0
            || turnType.IndexOf("PromResults", System.StringComparison.OrdinalIgnoreCase) >= 0
            || turnType.IndexOf("CampIntro", System.StringComparison.OrdinalIgnoreCase) >= 0
            || turnType.IndexOf("PlayerOrder", System.StringComparison.OrdinalIgnoreCase) >= 0
            || turnType == "Dates";
    }

    private static bool LooksLikeNavigationPick(string o1, string o2, string dialog)
    {
        var blob = (o1 + " " + o2 + " " + (dialog ?? "")).ToLowerInvariant();
        if (blob.IndexOf("choose your drink") >= 0)
            return true;
        if (blob.IndexOf("pick a drink") >= 0)
            return true;
        if (blob.IndexOf("prom king") >= 0 || blob.IndexOf("prom queen") >= 0)
            return true;
        return false;
    }
}
