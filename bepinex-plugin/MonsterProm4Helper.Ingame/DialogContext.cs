namespace MonsterProm4Helper.Ingame;

/// <summary>Filters real story dialogs from con navigation / cafeteria / shop turns.</summary>
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
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
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

        if (Contains(state.CurrentLocationName, "community")
            || Contains(state.TurnTypeName, "ConShop")
            || Contains(state.TurnTypeName, "Pitstop")
            || Contains(state.TurnTypeName, "Road")
            || Contains(state.TurnTypeName, "Destination")
            || Contains(state.TurnTypeName, "ChooseNpc")
            || Contains(state.TurnTypeName, "Comic"))
            state.InCafeteriaPhase = true;
    }

    public static bool IsMainGameEventDialog(LiveGameState live) => IsSchoolEventDialog(live);

    public static bool IsSchoolEventDialog(LiveGameState live)
    {
        if (!live.InSchool || live.InCafeteriaPhase)
            return false;

        if (!live.EventActive && live.ExchangeOptions.Count < 2)
            return false;

        if (live.ExchangeOptions.Count >= 2)
            return HasReadableOptions(live);

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 4 || o2.Length < 4)
            return false;

        if (LooksLikeNavigationPick(o1, o2, live.DialogText))
            return false;

        return (live.DialogText ?? "").Trim().Length >= 8;
    }

    public static bool IsPregameEventDialog(LiveGameState live)
    {
        if (!live.InPregame)
            return false;

        if (live.ExchangeOptions.Count >= 2)
            return HasReadableOptions(live);

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length >= 4 && o2.Length >= 4)
            return true;

        return live.EventActive && live.ChoiceCount >= 2;
    }

    public static bool IsChoiceDecisionActive(LiveGameState live)
    {
        if (live.InCafeteriaPhase)
            return false;
        if (!live.InSchool && !live.InPregame)
            return false;

        if (live.ExchangeOptions.Count >= 2)
            return true;

        if (HasReadableOptions(live))
            return true;

        if (live.InPregame && IsPregameEventDialog(live))
            return true;

        if (live.InSchool && IsSchoolEventDialog(live))
            return true;

        if (live.EventActive && live.ChoiceCount >= 2)
            return true;

        return false;
    }

    private static bool HasReadableOptions(LiveGameState live)
    {
        var count = 0;
        if ((live.Option1Text ?? "").Trim().Length >= 3)
            count++;
        if ((live.Option2Text ?? "").Trim().Length >= 3)
            count++;
        if ((live.Option3Text ?? "").Trim().Length >= 3)
            count++;
        return count >= 2;
    }

    private static bool IsNonEventTurn(string turnType)
    {
        if (string.IsNullOrEmpty(turnType))
            return false;

        return turnType == "Cafeteria"
            || turnType == "ChooseNpc"
            || turnType == "ChooseNpcEndings"
            || turnType == "ConShop"
            || turnType == "Pitstop"
            || turnType == "PitstopResults"
            || turnType == "Road"
            || turnType == "Destination"
            || turnType == "DestinationProgress"
            || turnType == "PlayerOrder"
            || turnType == "Comic"
            || turnType == "ComicEndings"
            || turnType == "Dates";
    }

    private static bool LooksLikeNavigationPick(string o1, string o2, string dialog)
    {
        var blob = (o1 + " " + o2 + " " + (dialog ?? "")).ToLowerInvariant();
        if (blob.IndexOf("pitstop") >= 0 || blob.IndexOf("destination") >= 0)
            return true;
        if (blob.IndexOf("con shop") >= 0 || blob.IndexOf("merch") >= 0)
            return true;
        if (blob.IndexOf("choose") >= 0 && blob.IndexOf("npc") >= 0)
            return true;
        return false;
    }

    private static bool Contains(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
            return false;
        return haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
