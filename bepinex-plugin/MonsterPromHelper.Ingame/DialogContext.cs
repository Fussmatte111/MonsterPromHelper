namespace MonsterPromHelper.Ingame;

/// <summary>Distinguishes story event dialogs from cafeteria / location picks.</summary>
public static class DialogContext
{
    public static void ReadSchoolPhase(GameManager gm, LiveGameState state)
    {
        state.TurnTypeName = "";
        state.InCafeteriaPhase = false;
        state.CurrentLocationName = "";

        if (gm == null)
            return;

        var turn = GameBridge.ReadMemberPublic(gm, "iCurrentTurnType");
        if (turn != null)
        {
            state.TurnTypeName = turn.ToString() ?? "";
            if (Contains(state.TurnTypeName, "Cafeteria"))
                state.InCafeteriaPhase = true;
        }

        var locDict = GameBridge.ReadMemberPublic(gm, "mPlayersCurrentLocation")
            as System.Collections.IDictionary;
        if (locDict == null)
            return;

        var playerKey = gm.CurrentPlayerColor;
        foreach (System.Collections.DictionaryEntry entry in locDict)
        {
            if (entry.Key == null || entry.Value == null)
                continue;
            if (!entry.Key.Equals(playerKey))
                continue;

            state.CurrentLocationName = entry.Value.ToString() ?? "";
            if (Contains(state.CurrentLocationName, "Cafeteria"))
                state.InCafeteriaPhase = true;
            break;
        }
    }

    public static bool IsSchoolEventDialog(LiveGameState live)
    {
        if (!live.InSchool || live.InCafeteriaPhase)
            return false;

        if (!live.EventActive)
            return false;

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 6 || o2.Length < 6)
            return false;

        if (LooksLikeCafeteriaPick(o1, o2, live.DialogText))
            return false;

        if (!MonoUtil.HasText(live.StatOption1) || !MonoUtil.HasText(live.StatOption2))
            return false;

        return (live.DialogText ?? "").Trim().Length >= 10;
    }

    private static bool LooksLikeCafeteriaPick(string o1, string o2, string dialog)
    {
        var blob = (o1 + " " + o2 + " " + (dialog ?? "")).ToLowerInvariant();
        if (blob.IndexOf("cafeteria") >= 0)
            return true;
        if (blob.IndexOf("sit with") >= 0 || blob.IndexOf("lunch table") >= 0)
            return true;
        if (blob.IndexOf("at the table") >= 0 || blob.IndexOf("grab lunch") >= 0)
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
