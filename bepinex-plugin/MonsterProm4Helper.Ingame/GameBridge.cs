using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterProm4Helper.Ingame;

/// <summary>Reads/writes Monster Prom 4 runtime state via reflection (many APIs are non-public).</summary>
public sealed class GameBridge
{
    public static readonly string[] RomanceNpcs =
    {
        "Liam", "Zoe", "Omen", "Doug", "Nico", "April",
    };

    private static readonly BindingFlags Inst =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly BindingFlags Static =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    private static MethodInfo? _getCurrentTurnType;
    private static MethodInfo? _isOptionSuccess;
    private static MethodInfo? _getCurrentPlayer;
    private static MethodInfo? _getPlayerById;
    private static MethodInfo? _getLovePointsToNpc;
    private static MethodInfo? _getDatesTimesTalked;
    private static MethodInfo? _setLovePoints;
    private static MethodInfo? _setStatPlayer;
    private static MethodInfo? _statsGetValue;
    private static MethodInfo? _getCntChoices;
    private static MethodInfo? _getStatForChoice;
    private static MethodInfo? _getChoicesScene;
    private static MethodInfo? _getNpc;

    public bool TryRead(out LiveGameState state, out string error)
    {
        state = new LiveGameState();
        error = "";

        try
        {
            EnsureMethods();

            state.SceneName = SceneManager.GetActiveScene().name;
            state.InSchool = IsMainGameScene(state.SceneName);
            state.InPregame = IsPregameScene(state.SceneName);

            var ctrl = FindSceneController(state.InSchool, state.InPregame);
            if (ctrl == null)
            {
                if (state.InSchool)
                    error = "MainGameSceneController noch nicht bereit.";
                else if (state.InPregame)
                    error = "PrologueSceneController noch nicht bereit.";
                return state.InSchool || state.InPregame;
            }

            var em = ReadEventManager(ctrl);
            var tm = ReadTurnManager(ctrl);
            if (em == null)
            {
                error = "Event-Manager fehlt.";
                return state.InSchool || state.InPregame;
            }

            object? player = null;
            var ptm = ReadMember(em, "mPlayerTurnManager");
            if (ptm != null && _getCurrentPlayer != null)
                player = _getCurrentPlayer.Invoke(ptm, null);

            if (player != null)
            {
                state.PlayerColor = ReadMember(player, "playerId")?.ToString() ?? "";
                if (!MonoUtil.HasText(state.PlayerColor) && state.InPregame)
                    state.PlayerColor = ReadMember(player, "playerName")?.ToString() ?? "Prologue";
                state.LockedInterest = ReadMember(player, "hardLockedInterest") as string ?? "";
                state.LoveThreshold = ReadLoveThreshold(em);
                if (tm != null)
                    DialogContext.ReadGamePhase(tm, player, state);
                ReadStats(player, state);
                ReadRomancePoints(player, state);
            }
            else if (state.InPregame)
            {
                state.PlayerColor = "Prologue";
            }

            state.EventActive = ReadBool(em, "mIsEventActive");
            var flow = ReadMember(em, "currEvent");
            var scene = ReadMember(em, "currScene");

            if (flow != null)
            {
                state.EventName = ReadMember(flow, "eventName") as string ?? "";
                state.ChoiceCount = InvokeInt(flow, _getCntChoices);
                if (tm != null)
                    ReadExchangeOptions(flow, tm, player, state);
            }

            ReadDialogTexts(scene, flow, state);
            if (tm != null && player != null)
                ApplyEnginePick(state, tm, flow, player);

            error = player != null ? "" : "Kein aktiver Spieler.";
            return DialogContext.IsChoiceDecisionActive(state)
                || player != null
                || state.EventActive
                || MonoUtil.HasText(state.EventName);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetStat(string playerName, string statKey, int value, out string error)
    {
        error = "";
        try
        {
            EnsureMethods();
            if (!TryParsePlayerId(playerName, out var playerId)
                || !TryParseStatKey(statKey, out var statEnum))
            {
                error = "Spieler oder Stat ungueltig";
                return false;
            }

            var sm = ResolveStatsManager();
            var player = ResolvePlayer(playerId);
            if (sm == null || player == null)
            {
                error = "StatsManager oder Spieler fehlt";
                return false;
            }

            _setStatPlayer.Invoke(sm, new object[] { player, statEnum, value, 0 });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetLove(string playerName, string npc, int value, out string error)
    {
        error = "";
        try
        {
            EnsureMethods();
            if (!TryParsePlayerId(playerName, out var playerId))
            {
                error = "Spieler-ID ungueltig";
                return false;
            }

            var player = ResolvePlayer(playerId);
            if (player == null)
            {
                error = "Spieler fehlt";
                return false;
            }

            _setLovePoints!.Invoke(player, new object[] { npc, value });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetInterest(string playerName, string npc, int value, out string error)
    {
        error = "MP4 nutzt LI-Auswahl statt Interest-Punkte (nicht editierbar).";
        return false;
    }

    public bool TryParsePlayerId(string name, out object playerId)
    {
        playerId = null!;
        if (string.IsNullOrEmpty(name))
            return false;

        var enumType = FindType("BeautifulGlitch.PlayerId");
        if (enumType == null)
            return false;

        try
        {
            playerId = Enum.Parse(enumType, name, true);
            return playerId.ToString() != "None" && playerId.ToString() != "Max";
        }
        catch
        {
            return false;
        }
    }

    public bool TryParseStatKey(string key, out object stat)
    {
        stat = null!;
        var enumType = FindType("BeautifulGlitchConfig.StatType");
        if (enumType == null)
            return false;

        switch ((key ?? "").ToUpperInvariant())
        {
            case "SMARTS": stat = Enum.Parse(enumType, "Smarts"); return true;
            case "BOLD": stat = Enum.Parse(enumType, "Boldness"); return true;
            case "CREATIVE": stat = Enum.Parse(enumType, "Creativity"); return true;
            case "CHARM": stat = Enum.Parse(enumType, "Charm"); return true;
            case "FUN": stat = Enum.Parse(enumType, "Fun"); return true;
            default: return false;
        }
    }

    internal static object ReadMemberPublic(object obj, string name) => ReadMember(obj, name);

    public static bool IsSchoolEventDialog(LiveGameState live) => DialogContext.IsMainGameEventDialog(live);

    public static bool IsOverlayEventDialog(LiveGameState live) =>
        DialogContext.IsChoiceDecisionActive(live);

    private static object? FindSceneController(bool inSchool, bool inPregame)
    {
        if (inSchool)
        {
            var main = UnityEngine.Object.FindObjectOfType(FindType("Game.MainGameSceneController"));
            if (main != null)
                return main;
        }

        if (inPregame)
        {
            var prologue = UnityEngine.Object.FindObjectOfType(FindType("Game.PrologueSceneController"));
            if (prologue != null)
                return prologue;

            var mgr = UnityEngine.Object.FindObjectOfType(FindType("Game.PrologueManager"));
            if (mgr != null)
                return mgr;
        }

        return null;
    }

    private static object? ReadEventManager(object ctrl)
    {
        var em = ReadMember(ctrl, "eventManager") ?? ReadMember(ctrl, "mEventManager");
        if (em != null)
            return em;

        if (ctrl != null && ctrl.GetType().Name.IndexOf("PrologueEventManager", StringComparison.OrdinalIgnoreCase) >= 0)
            return ctrl;

        var pem = UnityEngine.Object.FindObjectOfType(FindType("Game.PrologueEventManager"));
        if (pem == null)
            return null;

        return ReadMember(pem, "mEventManager") ?? pem;
    }

    private static object? ReadTurnManager(object ctrl)
    {
        return ReadMember(ctrl, "turnManager") ?? ReadMember(ctrl, "mTurnManager");
    }

    private static void EnsureMethods()
    {
        if (_getCurrentTurnType != null)
            return;

        var turnType = FindType("Game.TurnManager");
        var playerData = FindType("Game.PlayerData");
        var statsData = FindType("BeautifulGlitch.StatsData");
        var flowType = FindType("BeautifulGlitch.EventFlow");
        var statsMgr = FindType("BeautifulGlitch.PlayersStatsManager");
        var npcsType = FindType("BeautifulGlitch.Npcs");

        _getCurrentTurnType = turnType?.GetMethod("GetCurrentTurnType", Inst);
        _isOptionSuccess = turnType?.GetMethod("IsOptionSuccess", Inst);
        _getCurrentPlayer = FindType("Game.PlayerTurnManager")?.GetMethod("GetCurrentPlayer", Inst);
        _getPlayerById = FindType("Game.PlayerTurnManager")?.GetMethod("GetPlayerDataByPlayerId", Inst);
        _getLovePointsToNpc = playerData?.GetMethod("GetLovePointsToNpc", Inst);
        _getDatesTimesTalked = playerData?.GetMethod("GetDatesTimesTalked", Inst);
        _setLovePoints = playerData?.GetMethod("SetLovePoints", Inst);
        _setStatPlayer = statsMgr?.GetMethod("SetStatPlayer", Inst);
        _statsGetValue = statsData?.GetMethod("GetValue", Inst);
        _getCntChoices = flowType?.GetMethod("GetCntChoices", Inst);
        _getStatForChoice = flowType?.GetMethod("GetStatForChoice", Inst);
        _getChoicesScene = flowType?.GetMethod("GetChoicesScene", Inst);
        _getNpc = npcsType?.GetMethod("GetNpc", Inst);
    }

    private static void ReadStats(object player, LiveGameState state)
    {
        var stats = ReadMember(player, "stats");
        if (stats == null || _statsGetValue == null)
            return;

        var statType = FindType("BeautifulGlitchConfig.StatType");
        if (statType == null)
            return;

        foreach (var name in Enum.GetNames(statType))
        {
            if (name == "None")
                continue;
            var statEnum = Enum.Parse(statType, name);
            var key = ToStatKey(name);
            if (string.IsNullOrEmpty(key))
                continue;
            var val = _statsGetValue.Invoke(stats, new[] { statEnum });
            state.Stats[key] = val is int i ? i : 0;
        }
    }

    private static void ReadRomancePoints(object player, LiveGameState state)
    {
        for (var i = 0; i < RomanceNpcs.Length; i++)
        {
            var npc = RomanceNpcs[i];
            state.LovePoints[npc] = InvokeInt(player, _getLovePointsToNpc, npc);
            var profile = ResolveNpcProfile(npc);
            state.InterestPoints[npc] = profile != null
                ? InvokeInt(player, _getDatesTimesTalked, profile)
                : 0;
        }

        var dict = ReadMember(player, "npcLovePoints") as System.Collections.IDictionary;
        if (dict == null)
            return;

        foreach (System.Collections.DictionaryEntry entry in dict)
        {
            if (entry.Key == null)
                continue;
            var key = entry.Key.ToString() ?? "";
            if (entry.Value is int v && !state.LovePoints.ContainsKey(key))
                state.LovePoints[key] = v;
        }
    }

    private static object? ResolveNpcProfile(string npcName)
    {
        try
        {
            var ctrl = UnityEngine.Object.FindObjectOfType(FindType("Game.MainGameSceneController"));
            if (ctrl == null)
                return null;
            var em = ReadMember(ctrl, "eventManager");
            var npcs = ReadMember(em, "mNpcs");
            if (npcs == null || _getNpc == null)
                return null;
            return _getNpc.Invoke(npcs, new object[] { npcName });
        }
        catch
        {
            return null;
        }
    }

    private static void ReadExchangeOptions(object flow, object tm, object player, LiveGameState state)
    {
        state.ExchangeOptions.Clear();
        var count = InvokeInt(flow, _getCntChoices);
        if (count <= 0 || _getStatForChoice == null)
            return;

        for (var option = 1; option <= count; option++)
        {
            var gainEnum = _getStatForChoice.Invoke(flow, new object[] { option });
            var loseEnum = ResolveLoseStat(flow, option, count);
            var info = new ExchangeOptionInfo
            {
                Option = option,
                GainStat = ToStatKey(gainEnum?.ToString() ?? ""),
                LoseStat = ToStatKey(loseEnum?.ToString() ?? ""),
                IsSuccess = InvokeBool(tm, _isOptionSuccess, option, flow),
            };
            state.ExchangeOptions.Add(info);

            if (option == 1)
                state.StatOption1 = info.GainStat;
            else if (option == 2)
                state.StatOption2 = info.GainStat;
        }
    }

    private static object? ResolveLoseStat(object flow, int option, int count)
    {
        if (count == 2 && _getStatForChoice != null)
        {
            var other = option == 1 ? 2 : 1;
            return _getStatForChoice.Invoke(flow, new object[] { other });
        }

        return null;
    }

    private static void ReadDialogTexts(object? scene, object? flow, LiveGameState state)
    {
        var choiceScene = scene;
        if (flow != null && _getChoicesScene != null)
        {
            var dedicated = _getChoicesScene.Invoke(flow, null);
            if (dedicated != null)
                choiceScene = dedicated;
        }

        if (scene != null)
        {
            var modified = ReadMember(scene, "modifiedText") as string;
            var main = ReadMember(scene, "text") as string;
            state.DialogText = MonoUtil.HasText(modified) ? modified : (main ?? "");
        }

        if (choiceScene == null)
            return;

        state.Option1Text = ReadMember(choiceScene, "textChoice2") as string ?? "";
        state.Option2Text = ReadMember(choiceScene, "textChoice3") as string ?? "";
        state.Option3Text = ReadMember(choiceScene, "textChoice4") as string ?? "";
    }

    private static void ApplyEnginePick(LiveGameState state, object tm, object? flow, object player)
    {
        if (flow == null || state.ExchangeOptions.Count == 0)
            return;

        for (var i = 0; i < state.ExchangeOptions.Count; i++)
        {
            var opt = state.ExchangeOptions[i];
            if (!opt.IsSuccess)
                continue;

            state.EnginePick = opt.Option;
            state.EngineStat = opt.GainStat;
            return;
        }

        if (state.ExchangeOptions.Count != 2)
            return;

        var a = state.ExchangeOptions[0];
        var b = state.ExchangeOptions[1];
        if (string.IsNullOrEmpty(a.GainStat) || string.IsNullOrEmpty(b.GainStat))
            return;

        if (!state.Stats.TryGetValue(a.GainStat, out var va)
            || !state.Stats.TryGetValue(b.GainStat, out var vb))
            return;

        if (va > vb)
        {
            state.EnginePick = a.Option;
            state.EngineStat = a.GainStat;
        }
        else if (vb > va)
        {
            state.EnginePick = b.Option;
            state.EngineStat = b.GainStat;
        }
        else
        {
            state.EnginePick = 0;
            state.EngineStat = "gleich";
        }
    }

    private static int ReadLoveThreshold(object em)
    {
        var profile = ReadMember(em, "mDifficultyConditions");
        if (profile == null)
            return 40;
        var v = ReadMember(profile, "lovePointsNeeded");
        return v is int i && i > 0 ? i : 40;
    }

    private static object? ResolveStatsManager()
    {
        var ctrl = UnityEngine.Object.FindObjectOfType(FindType("Game.MainGameSceneController"));
        if (ctrl == null)
            return null;
        var em = ReadMember(ctrl, "eventManager");
        return ReadMember(em, "mPlayersStatsManager");
    }

    private static object? ResolvePlayer(object playerId)
    {
        var ctrl = UnityEngine.Object.FindObjectOfType(FindType("Game.MainGameSceneController"));
        if (ctrl == null)
            return null;
        var em = ReadMember(ctrl, "eventManager");
        var ptm = ReadMember(em, "mPlayerTurnManager");
        if (ptm == null || _getPlayerById == null)
            return null;
        return _getPlayerById.Invoke(ptm, new[] { playerId });
    }

    private static bool IsMainGameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        if (sceneName.IndexOf("MainGame", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }

    private static bool IsPregameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;
        if (sceneName.IndexOf("Prologue", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
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

    private static object? ReadMember(object obj, string name)
    {
        if (obj == null)
            return null;
        var t = obj.GetType();
        var f = t.GetField(name, Inst);
        if (!MonoUtil.IsNull(f))
            return f.GetValue(obj);
        var p = t.GetProperty(name, Inst);
        if (!MonoUtil.IsNull(p) && p.CanRead)
            return p.GetValue(obj, null);
        return null;
    }

    private static bool ReadBool(object obj, string name)
    {
        var v = ReadMember(obj, name);
        return v is bool b && b;
    }

    private static int InvokeInt(object obj, MethodInfo? method, params object[] args)
    {
        if (obj == null || method == null)
            return 0;
        var v = method.Invoke(obj, args);
        return v is int i ? i : 0;
    }

    private static bool InvokeBool(object obj, MethodInfo? method, params object[] args)
    {
        if (obj == null || method == null)
            return false;
        var v = method.Invoke(obj, args);
        return v is bool b && b;
    }

    internal static string ToStatKey(string statName)
    {
        if (string.IsNullOrEmpty(statName))
            return "";
        switch (statName)
        {
            case "Smarts": return "SMARTS";
            case "Boldness": return "BOLD";
            case "Creativity": return "CREATIVE";
            case "Charm": return "CHARM";
            case "Fun": return "FUN";
            default: return "";
        }
    }
}

/// <summary>Marker type removed — scene objects are read via reflection.</summary>
internal sealed class EventSceneRef { }

public sealed class ExchangeOptionInfo
{
    public int Option;
    public string GainStat = "";
    public string LoseStat = "";
    public bool IsSuccess;
}

public sealed class LiveGameState
{
    public string SceneName = "";
    public bool InSchool;
    public bool InPregame;
    public bool InCafeteriaPhase;
    public string TurnTypeName = "";
    public string CurrentLocationName = "";
    public bool EventActive;
    public int EventIndex = -1;
    public string EventName = "";
    public int ChoiceCount;
    public string PlayerColor = "";
    public string LockedInterest = "";
    public int LoveThreshold = 40;
    public string DialogText = "";
    public string Option1Text = "";
    public string Option2Text = "";
    public string Option3Text = "";
    public string StatOption1 = "";
    public string StatOption2 = "";
    public int EnginePick;
    public string EngineStat = "";
    public List<ExchangeOptionInfo> ExchangeOptions = new List<ExchangeOptionInfo>();
    public Dictionary<string, int> Stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> LovePoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> InterestPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
