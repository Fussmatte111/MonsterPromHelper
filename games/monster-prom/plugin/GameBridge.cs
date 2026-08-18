using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterPromHelper.Ingame;

/// <summary>Reads/writes Monster Prom runtime state (stats, love, interest).</summary>
public sealed class GameBridge
{
    public static readonly string[] RomanceNpcs =
    {
        "Damien", "Liam", "Miranda", "Polly", "Scott", "Vera", "Calculester", "Zoe",
    };

    private static readonly BindingFlags Inst =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public bool TryRead(out LiveGameState state, out string error)
    {
        state = new LiveGameState();
        error = "";

        try
        {
            state.SceneName = SceneManager.GetActiveScene().name;
            state.InSchool = state.SceneName == "InGame_School";

            var gm = GameManager.Instance;
            if (gm == null)
            {
                error = "GameManager not ready yet.";
                return false;
            }

            state.PlayerColor = gm.CurrentPlayerColor.ToString();
            state.LockedInterest = gm.GetPlayerLockedInterest(gm.CurrentPlayerColor) ?? "";
            state.LoveThreshold = ReadIntMember(gm, "LovePointsNeededForGoodRelationship", 40);
            DialogContext.ReadSchoolPhase(gm, state);

            ReadRomancePoints(gm, gm.CurrentPlayerColor, state);

            var sm = StatsManager.Instance;
            if (sm != null)
                ReadStats(sm, gm.CurrentPlayerColor, state);

            var em = EventManager.Instance;
            if (em == null)
            {
                error = "EventManager not ready yet.";
                return state.InSchool;
            }

            state.EventActive = ReadBool(em, "mEventActive");
            state.EventIndex = ReadInt(em, "mEventIndexActive");

            var events = em.Events;
            if (events != null && state.EventIndex >= 0 && state.EventIndex < events.Length)
            {
                var flow = events[state.EventIndex];
                if (flow != null)
                {
                    state.EventName = flow.EventName ?? "";
                    state.StatOption1 = ToStatKey(flow.StatRequired_Option1);
                    state.StatOption2 = ToStatKey(flow.StatRequired_Option2);
                }
            }

            state.DialogText = ReadLabel(em.EventTextLabel);
            state.Option1Text = ReadLabel(em.EventTextOption1Label);
            state.Option2Text = ReadLabel(em.EventTextOption2Label);

            ApplyEnginePick(state);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetStat(NGameConstants.EPlayerColor player, NGameConstants.EStat stat, int value, out string error)
    {
        error = "";
        try
        {
            var sm = StatsManager.Instance;
            if (sm == null)
            {
                error = "StatsManager missing";
                return false;
            }

            var cur = sm.GetStatInt(player, stat);
            var delta = value - cur;
            if (delta > 0)
                sm.IncreaseStat(player, stat, delta);
            else if (delta < 0)
                sm.DecreaseStat(player, stat, -delta, false);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetLove(NGameConstants.EPlayerColor player, string npc, int value, out string error)
    {
        error = "";
        try
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                error = "GameManager missing";
                return false;
            }

            var outer = ReadMember(gm, "mPlayersLovePoints") as Dictionary<NGameConstants.EPlayerColor, Dictionary<string, int>>;
            if (outer == null)
            {
                error = "Love dictionary missing";
                return false;
            }

            Dictionary<string, int> inner;
            if (!outer.TryGetValue(player, out inner) || inner == null)
            {
                inner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                outer[player] = inner;
            }

            inner[npc] = value;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TrySetInterest(NGameConstants.EPlayerColor player, string npc, int value, out string error)
    {
        error = "";
        try
        {
            var gm = GameManager.Instance;
            if (gm == null)
            {
                error = "GameManager missing";
                return false;
            }

            gm.SetInterestPointsFromPlayerToNpc(player, npc, value, false);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool TryParsePlayerColor(string name, out NGameConstants.EPlayerColor color)
    {
        color = NGameConstants.EPlayerColor.Blue;
        if (string.IsNullOrEmpty(name))
            return false;
        try
        {
            color = (NGameConstants.EPlayerColor)Enum.Parse(typeof(NGameConstants.EPlayerColor), name, true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool TryParseStatKey(string key, out NGameConstants.EStat stat)
    {
        switch ((key ?? "").ToUpperInvariant())
        {
            case "SMARTS": stat = NGameConstants.EStat.Smarts; return true;
            case "BOLD": stat = NGameConstants.EStat.Boldness; return true;
            case "CREATIVE": stat = NGameConstants.EStat.Creativity; return true;
            case "CHARM": stat = NGameConstants.EStat.Charm; return true;
            case "FUN": stat = NGameConstants.EStat.Fun; return true;
            case "MONEY": stat = NGameConstants.EStat.Money; return true;
            default:
                stat = NGameConstants.EStat.Smarts;
                return false;
        }
    }

    private static void ReadStats(StatsManager sm, NGameConstants.EPlayerColor color, LiveGameState state)
    {
        foreach (NGameConstants.EStat stat in Enum.GetValues(typeof(NGameConstants.EStat)))
        {
            var key = ToStatKey(stat);
            if (string.IsNullOrEmpty(key))
                continue;
            state.Stats[key] = sm.GetStatInt(color, stat);
        }
    }

    private static void ReadRomancePoints(GameManager gm, NGameConstants.EPlayerColor player, LiveGameState state)
    {
        for (var i = 0; i < RomanceNpcs.Length; i++)
        {
            var npc = RomanceNpcs[i];
            state.LovePoints[npc] = gm.GetLovePointsFromPlayerToNpc(player, npc);
            state.InterestPoints[npc] = gm.GetInterestPointsFromPlayerToNpc(player, npc);
        }

        var outer = ReadMember(gm, "mPlayersLovePoints") as Dictionary<NGameConstants.EPlayerColor, Dictionary<string, int>>;
        if (outer == null || !outer.TryGetValue(player, out var inner) || inner == null)
            return;

        foreach (var kv in inner)
        {
            if (!state.LovePoints.ContainsKey(kv.Key))
                state.LovePoints[kv.Key] = kv.Value;
        }
    }

    private static void ApplyEnginePick(LiveGameState state)
    {
        if (string.IsNullOrEmpty(state.StatOption1) || string.IsNullOrEmpty(state.StatOption2))
            return;
        if (!state.Stats.TryGetValue(state.StatOption1, out var v1)
            || !state.Stats.TryGetValue(state.StatOption2, out var v2))
            return;

        if (v1 > v2)
        {
            state.EnginePick = 1;
            state.EngineStat = state.StatOption1;
        }
        else if (v2 > v1)
        {
            state.EnginePick = 2;
            state.EngineStat = state.StatOption2;
        }
        else
        {
            state.EnginePick = 0;
            state.EngineStat = "gleich";
        }
    }

    private static string ReadLabel(UILabel label)
    {
        if (MonoUtil.IsNull(label))
            return "";
        return label.text ?? "";
    }

    private static bool ReadBool(object obj, string name)
    {
        var v = ReadMember(obj, name);
        return v is bool b && b;
    }

    private static int ReadInt(object obj, string name)
    {
        var v = ReadMember(obj, name);
        return v is int i ? i : 0;
    }

    private static int ReadIntMember(object obj, string name, int fallback)
    {
        var v = ReadMember(obj, name);
        return v is int i ? i : fallback;
    }

    internal static object ReadMemberPublic(object obj, string name) => ReadMember(obj, name);

    public static bool IsSchoolEventDialog(LiveGameState live) => DialogContext.IsSchoolEventDialog(live);

    private static object ReadMember(object obj, string name)
    {
        var t = obj.GetType();
        var f = t.GetField(name, Inst);
        if (!MonoUtil.IsNull(f))
            return f.GetValue(obj);
        var p = t.GetProperty(name, Inst);
        if (!MonoUtil.IsNull(p) && p.CanRead)
            return p.GetValue(obj, null);
        return null;
    }

    private static string ToStatKey(NGameConstants.EStat stat)
    {
        switch (stat)
        {
            case NGameConstants.EStat.Smarts: return "SMARTS";
            case NGameConstants.EStat.Boldness: return "BOLD";
            case NGameConstants.EStat.Creativity: return "CREATIVE";
            case NGameConstants.EStat.Charm: return "CHARM";
            case NGameConstants.EStat.Fun: return "FUN";
            case NGameConstants.EStat.Money: return "MONEY";
            default: return "";
        }
    }
}

public sealed class LiveGameState
{
    public string SceneName = "";
    public bool InSchool;
    public bool InCafeteriaPhase;
    public string TurnTypeName = "";
    public string CurrentLocationName = "";
    public bool EventActive;
    public int EventIndex = -1;
    public string EventName = "";
    public string PlayerColor = "";
    public string LockedInterest = "";
    public int LoveThreshold = 40;
    public string DialogText = "";
    public string Option1Text = "";
    public string Option2Text = "";
    public string StatOption1 = "";
    public string StatOption2 = "";
    public int EnginePick;
    public string EngineStat = "";
    public Dictionary<string, int> Stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> LovePoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> InterestPoints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
}
