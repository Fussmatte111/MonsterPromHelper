using System.Collections.Generic;
using UnityEngine;

namespace MonsterProm4Helper.Ingame;

public sealed class SecretAlertMatch
{
    public string EventName = "";
    public string Route = "";
    public string WikiTitle = "";
    public int ChainStep;
    public int ChainTotal;
    public int MatchedOption;
    public string MatchedText = "";
    public string DedupeKey = "";

    public string Title => "Secret Ending!";

    public string Body
    {
        get
        {
            var lines = new List<string>();
            if (MonoUtil.HasText(WikiTitle))
                lines.Add(WikiTitle);
            else if (MonoUtil.HasText(Route))
                lines.Add(Route);

            if (ChainTotal > 0 && ChainStep > 0)
                lines.Add("Schritt " + ChainStep + "/" + ChainTotal);

            if (MonoUtil.HasText(EventName))
                lines.Add("Event: " + EventName);

            if (MonoUtil.HasText(MatchedText))
                lines.Add(Truncate(MatchedText, 64));

            if (lines.Count == 0)
                return EventName;
            return JoinLines(lines);
        }
    }

    private static string JoinLines(List<string> lines)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                sb.Append('\n');
            sb.Append(lines[i]);
        }
        return sb.ToString();
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
            return text ?? "";
        return text.Substring(0, max - 3) + "...";
    }
}

/// <summary>Toast when a secret-ending dialog is confidently matched.</summary>
public static class SecretEndingAlerts
{
    private static readonly HashSet<string> SeenKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    private static float _toastUntil;
    private static float _nextAlertAllowed;
    private static string _toastTitle = "";
    private static string _toastBody = "";
    private static string _lastDialogFingerprint = "";

    public static bool HasActiveToast => Time.unscaledTime < _toastUntil;

    public static void ResetScene()
    {
        _toastUntil = 0f;
        SeenKeys.Clear();
        _lastDialogFingerprint = "";
    }

    public static void Tick(EventDb db, GameBridge bridge, bool enabled, bool firstOnly)
    {
        if (!enabled || db == null)
            return;

        LiveGameState live;
        string err;
        if (!bridge.TryRead(out live, out err))
            return;

        if (!live.InSchool || !GameBridge.IsSchoolEventDialog(live))
            return;

        var o1 = (live.Option1Text ?? "").Trim();
        var o2 = (live.Option2Text ?? "").Trim();
        if (o1.Length < 10 || o2.Length < 10)
            return;

        var fingerprint = (live.EventName ?? "") + "||" + o1.ToLowerInvariant() + "||" + o2.ToLowerInvariant();
        if (fingerprint == _lastDialogFingerprint)
            return;

        SecretAlertMatch match;
        try
        {
            match = db.TryDetectSecret(live);
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError("Secret-Check: " + ex.Message);
            return;
        }

        if (match == null)
            return;

        _lastDialogFingerprint = fingerprint;

        if (Time.unscaledTime < _nextAlertAllowed)
            return;

        if (firstOnly && SeenKeys.Contains(match.DedupeKey))
            return;

        SeenKeys.Add(match.DedupeKey);
        _toastTitle = match.Title;
        _toastBody = match.Body;
        _toastUntil = Time.unscaledTime + 10f;
        _nextAlertAllowed = Time.unscaledTime + 25f;

        Plugin.Log.LogInfo("[SECRET] " + match.Body.Replace("\n", " | "));
    }

    public static void SyncUi()
    {
        if (!HasActiveToast)
        {
            NguiOverlayView.HideSecretToast();
            return;
        }

        NguiOverlayView.ShowSecretToast(_toastTitle, _toastBody);
    }

    public static void DrawToast()
    {
        SyncUi();
    }
}
