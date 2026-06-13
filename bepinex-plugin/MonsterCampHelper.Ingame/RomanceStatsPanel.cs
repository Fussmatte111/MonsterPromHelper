using System.Collections.Generic;
using UnityEngine;

namespace MonsterCampHelper.Ingame;

/// <summary>Edit player stats + love in the overlay (cheat panel).</summary>
public static class RomanceStatsPanel
{
    private static readonly Dictionary<string, string> StatDraft = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> LoveDraft = new Dictionary<string, string>();
    private static string _status = "";
    private static float _statusUntil;

    public static void SyncDrafts(LiveGameState live)
    {
        for (var i = 0; i < EventDb.StatKeys.Length; i++)
        {
            var key = EventDb.StatKeys[i];
            var val = live.Stats.TryGetValue(key, out var v) ? v : 0;
            StatDraft[key] = val.ToString();
        }

        for (var i = 0; i < GameBridge.RomanceNpcs.Length; i++)
        {
            var npc = GameBridge.RomanceNpcs[i];
            var love = live.LovePoints.TryGetValue(npc, out var lv) ? lv : 0;
            LoveDraft[npc] = love.ToString();
        }
    }

    public static void Draw(ref float y, float w, LiveGameState live, GameBridge bridge, bool editable)
    {
        y += 10f;
        DrawSectionTitle(ref y, w, "—— Stats & Zuneigung ——");
        DrawSectionTitle(ref y, w, "Stats (" + live.PlayerColor + ")");
        if (!editable)
            DrawMuted(ref y, w, "Bearbeiten nur in der Camp-Runde (MainGame).");

        for (var i = 0; i < EventDb.StatKeys.Length; i++)
        {
            var key = EventDb.StatKeys[i];
            if (!StatDraft.ContainsKey(key))
                StatDraft[key] = "0";

            GUI.Label(new Rect(0, y, 70, 20), key, OverlayGui.MutedStyle);
            if (editable)
            {
                StatDraft[key] = GUI.TextField(new Rect(72, y, 48, 22), StatDraft[key]);
                if (GUI.Button(new Rect(124, y, 28, 22), "-"))
                {
                    NudgeDraft(StatDraft, key, -1);
                    ApplyStat(key, bridge, live);
                }

                if (GUI.Button(new Rect(154, y, 28, 22), "+"))
                {
                    NudgeDraft(StatDraft, key, 1);
                    ApplyStat(key, bridge, live);
                }
                if (GUI.Button(new Rect(186, y, 56, 22), "Set"))
                    ApplyStat(key, bridge, live);
            }
            else
            {
                var val = live.Stats.TryGetValue(key, out var v) ? v : 0;
                GUI.Label(new Rect(72, y, 80, 20), val.ToString(), OverlayGui.BodyStyle);
            }

            y += 24f;
        }

        y += 8f;
        DrawSectionTitle(ref y, w, "Zuneigung (Love)");
        DrawMuted(ref y, w, "Gut: ca. " + live.LoveThreshold + "+ Love  |  * = gesperrter LI: "
            + (MonoUtil.HasText(live.LockedInterest) ? live.LockedInterest : "—"));
        DrawMuted(ref y, w, "Dates-Zaehler = wie oft mit LI gesprochen (nur Anzeige).");

        for (var i = 0; i < GameBridge.RomanceNpcs.Length; i++)
        {
            var npc = GameBridge.RomanceNpcs[i];
            if (!LoveDraft.ContainsKey(npc))
                LoveDraft[npc] = "0";

            var isLocked = MonoUtil.HasText(live.LockedInterest)
                && string.Equals(live.LockedInterest, npc, System.StringComparison.OrdinalIgnoreCase);

            var label = npc + (isLocked ? " *" : "");
            GUI.Label(new Rect(0, y, 90, 20), label, isLocked ? OverlayGui.PickStyle : OverlayGui.MutedStyle);

            if (editable)
            {
                GUI.Label(new Rect(0, y + 20, 36, 18), "Love", OverlayGui.MutedStyle);
                LoveDraft[npc] = GUI.TextField(new Rect(38, y + 18, 40, 22), LoveDraft[npc]);
                if (GUI.Button(new Rect(80, y + 18, 26, 22), "-"))
                {
                    NudgeDraft(LoveDraft, npc, -1);
                    ApplyLove(npc, bridge, live);
                }

                if (GUI.Button(new Rect(108, y + 18, 26, 22), "+"))
                {
                    NudgeDraft(LoveDraft, npc, 1);
                    ApplyLove(npc, bridge, live);
                }

                if (GUI.Button(new Rect(136, y + 18, 36, 22), "Set"))
                    ApplyLove(npc, bridge, live);

                var dates = live.InterestPoints.TryGetValue(npc, out var dt) ? dt : 0;
                GUI.Label(new Rect(178, y + 20, 120, 18), "Dates: " + dates, OverlayGui.MutedStyle);
            }
            else
            {
                var loveNow = live.LovePoints.TryGetValue(npc, out var l) ? l : 0;
                var datesNow = live.InterestPoints.TryGetValue(npc, out var d) ? d : 0;
                GUI.Label(new Rect(92, y, 180, 20), "Love " + loveNow + "  |  Dates " + datesNow, OverlayGui.BodyStyle);
            }

            y += 44f;
        }

        if (Time.unscaledTime < _statusUntil && MonoUtil.HasText(_status))
        {
            y += 6f;
            GUI.Label(new Rect(0, y, w, 32f), _status, OverlayGui.PickStyle);
            y += 34f;
        }
    }

    private static void ApplyStat(string key, GameBridge bridge, LiveGameState live)
    {
        object stat;
        if (!bridge.TryParsePlayerId(live.PlayerColor, out _)
            || !bridge.TryParseStatKey(key, out stat))
        {
            ShowStatus("Stat ungueltig");
            return;
        }

        int value;
        if (!int.TryParse(StatDraft[key], out value))
        {
            ShowStatus("Zahl fuer " + key + " ungueltig");
            return;
        }

        string err;
        if (!bridge.TrySetStat(live.PlayerColor, key, value, out err))
        {
            ShowStatus(err);
            return;
        }

        ShowStatus(key + " = " + value);
        Plugin.Instance.RefreshLiveState(true);
    }

    private static void ApplyLove(string npc, GameBridge bridge, LiveGameState live)
    {
        if (!bridge.TryParsePlayerId(live.PlayerColor, out _))
        {
            ShowStatus("Spieler-ID unbekannt");
            return;
        }

        int value;
        if (!int.TryParse(LoveDraft[npc], out value))
        {
            ShowStatus("Love-Zahl ungueltig");
            return;
        }

        string err;
        if (!bridge.TrySetLove(live.PlayerColor, npc, value, out err))
        {
            ShowStatus(err);
            return;
        }

        ShowStatus("Love " + npc + " = " + value);
        Plugin.Instance.RefreshLiveState(true);
    }

    private static void NudgeDraft(Dictionary<string, string> draft, string key, int delta)
    {
        int v;
        if (!int.TryParse(draft[key], out v))
            v = 0;
        draft[key] = (v + delta).ToString();
    }

    private static void ShowStatus(string msg)
    {
        _status = msg;
        _statusUntil = Time.unscaledTime + 2.5f;
    }

    private static void DrawSectionTitle(ref float y, float w, string text)
    {
        GUI.Label(new Rect(0, y, w, 22f), text, OverlayGui.TitleStyle);
        y += 24f;
    }

    private static void DrawMuted(ref float y, float w, string text)
    {
        GUI.Label(new Rect(0, y, w, 18f), text, OverlayGui.MutedStyle);
        y += 20f;
    }
}
