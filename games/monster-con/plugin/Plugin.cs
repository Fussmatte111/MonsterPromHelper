using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterProm4Helper.Ingame;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static Plugin Instance = null!;
    internal static Harmony Harmony = null!;

    internal static bool OverlayOpen;
    internal static int LastToggleFrame = -1;

    private static bool _bootstrapped;
    private static bool _shutdownDone;

    private EventDb _db = null!;
    private readonly GameBridge _bridge = new GameBridge();
    private ConfigEntry<KeyCode> _toggleKey = null!;
    private ConfigEntry<KeyCode> _toggleKeyAlt = null!;
    private ConfigEntry<bool> _secretAlerts = null!;
    private ConfigEntry<bool> _secretAlertsFirstOnly = null!;
    private ConfigEntry<bool> _pickHint = null!;

    private string _status = "";
    private int _lastToggleFrame = -1;
    private LiveGameState _live = new LiveGameState();
    private OverlayViewModel _view = new OverlayViewModel();
    private float _nextPoll;
    private float _hudUntil;
    private float _toggleLockUntil;
    private float _nextSecretCheck;
    private int _tickFrameGuard = -1;

    private void Awake()
    {
        if (_bootstrapped && Instance != null && Instance != this)
        {
            Log.LogWarning("Removed a second plugin instance (causes ON/OFF flicker).");
            Destroy(this);
            return;
        }

        _bootstrapped = true;
        Instance = this;
        Log = Logger;
        Harmony = new Harmony(PluginInfo.PLUGIN_GUID);

        try
        {
            var pluginDir = IoUtil.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(pluginDir))
                pluginDir = Paths.PluginPath;
            var dataDir = EventDb.ResolveDataFolder(pluginDir);
            Directory.CreateDirectory(dataDir);

            _db = new EventDb();
            _db.LoadFromFolder(dataDir);
            Log.LogInfo("Plugin path: " + pluginDir);
            Log.LogInfo(
                "Data: "
                + dataDir
                + " ("
                + _db.EventCount
                + " Events, "
                + _db.PregamePickCount
                + " Likes, "
                + _db.PregameCharacterCount
                + " LI-Profile)");

            _toggleKey = Config.Bind("Overlay", "ToggleKey", KeyCode.F8, "Toggle overlay");
            _toggleKeyAlt = Config.Bind("Overlay", "ToggleKeyAlt", KeyCode.F9, "Toggle overlay (alternate)");
            _secretAlerts = Config.Bind("Alerts", "SecretEndingToast", true, "Secret ending toast");
            _secretAlertsFirstOnly = Config.Bind("Alerts", "SecretEndingFirstOnly", true, "Secret toast only once per route");
            _pickHint = Config.Bind("Alerts", "PickHint", true, "Pick hint without F8");

            if (_toggleKey.Value == _toggleKeyAlt.Value)
                _toggleKeyAlt.Value = KeyCode.F9;

            _status = "F8 = overlay (F9 alternate)";
            _hudUntil = Time.unscaledTime + 120f;
            OverlayOpen = false;

            OverlayHost.EnsureExists();
            PluginTickPatch.TryApply();

            NguiInputBlockPatch.TryApply();
            OverlayCursorPatch.TryApply();
            NguiOverlayPatch.TryApply();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnApplicationQuitting;

            Log.LogInfo("Helper v" + PluginInfo.PLUGIN_VERSION + " — NGUI-Overlay (F8/F9), tick hook active");
        }
        catch (Exception ex)
        {
            Log.LogError("Awake failed: " + ex);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NguiOverlayView.ResetForNewScene();
        ChoiceHintBadges.ResetScene();
        SecretEndingAlerts.ResetScene();
        PickHintToast.ResetScene();
        RefreshOverlayData();
        SyncUi();
    }

    private void OnApplicationQuitting() => ShutdownForQuit();

    internal static void ShutdownForQuit()
    {
        if (_shutdownDone)
            return;

        _shutdownDone = true;
        OverlayOpen = false;
        NguiInputGate.Restore();
        GameInputLock.Release();
        NguiOverlayView.Hide();
    }

    private void Update()
    {
        if (Instance != this)
            return;
        TickFrame();
    }

    internal void TickFrame()
    {
        if (Instance != this)
            return;
        if (_tickFrameGuard == Time.frameCount)
            return;
        _tickFrameGuard = Time.frameCount;

        NativeKeyPoll.Tick(this);
        PollToggleKeyDown();
        NguiInputGate.Sync();
        GameInputLock.Sync();
        TickSecretAlerts();
        TickPickHint();

        if (OverlayOpen)
        {
            OverlayCursor.EnsureVisible();
            if (Time.unscaledTime >= _nextPoll)
            {
                _nextPoll = Time.unscaledTime + 0.15f;
                RefreshOverlayData();
            }
        }

        SyncUi();
    }

    private void SyncUi()
    {
        if (_db == null)
            return;

        NguiOverlayView.Sync(OverlayOpen, Time.unscaledTime < _hudUntil, _view, _live, _bridge);
        if (!OverlayOpen)
            ChoiceHintBadges.Sync(_bridge, _db);
        PickHintToast.SyncUi();
        SecretEndingAlerts.SyncUi();
    }

    private void PollToggleKeyDown()
    {
        if (_toggleKey == null || Time.unscaledTime < _toggleLockUntil)
            return;

        if (Input.GetKeyDown(_toggleKey.Value))
            TryHandleToggleKey(_toggleKey.Value, "Input");
        else if (_toggleKeyAlt.Value != _toggleKey.Value && Input.GetKeyDown(_toggleKeyAlt.Value))
            TryHandleToggleKey(_toggleKeyAlt.Value, "Input");
    }

    internal void TryHandleToggleKey(KeyCode key, string source)
    {
        if (_toggleKey == null || Time.unscaledTime < _toggleLockUntil)
            return;
        if (key != _toggleKey.Value && key != _toggleKeyAlt.Value)
            return;
        if (_lastToggleFrame == Time.frameCount)
            return;

        _lastToggleFrame = Time.frameCount;
        _toggleLockUntil = Time.unscaledTime + 0.35f;
        ApplyToggle(key, source);
    }

    private void ApplyToggle(KeyCode viaKey, string source)
    {
        OverlayOpen = !OverlayOpen;
        Log.LogInfo(OverlayOpen ? "Overlay ON (" + viaKey + ", " + source + ")" : "Overlay OFF (" + viaKey + ")");

        if (!OverlayOpen)
        {
            NguiInputGate.Restore();
            GameInputLock.Release();
            return;
        }

        OverlayCursor.EnsureVisible();
        _hudUntil = -1f;
        _nextPoll = 0;
        RefreshOverlayData();
        Log.LogInfo(
            "Overlay NGUI — scene="
            + _view.SceneName
            + ", stats editable="
            + CanEditStats(_live)
            + ", player="
            + (MonoUtil.HasText(_live.PlayerColor) ? _live.PlayerColor : "—"));
    }

    internal void NudgeStat(string key, int delta)
    {
        if (!OverlayOpen)
            return;

        if (!CanEditStats(_live))
        {
            Log.LogInfo("Stat +/- only during the Con round (MainGame, player active).");
            return;
        }

        int cur;
        if (!_live.Stats.TryGetValue(key, out cur))
            cur = 0;

        string err;
        if (!_bridge.TrySetStat(_live.PlayerColor, key, cur + delta, out err))
        {
            Log.LogWarning("Stat: " + err);
            return;
        }

        RefreshLiveState(true);
    }

    private bool liveInGame() => _live.InSchool;

    internal static bool CanEditStats(LiveGameState live)
    {
        if (live.InCafeteriaPhase)
            return false;
        if (!MonoUtil.HasText(live.PlayerColor))
            return false;
        return live.InSchool || live.InPregame;
    }

    internal void RefreshLiveState(bool syncEditorDrafts)
    {
        RefreshOverlayData();
    }

    private void RefreshOverlayData()
    {
        if (_db == null)
            return;

        string err;
        if (!_bridge.TryRead(out _live, out err))
            _status = err;
        else if (_live.EventActive)
            _status = "Event active — pick below";
        else if (_live.InSchool)
            _status = "At the Con — waiting for dialog";
        else if (_live.InPregame)
            _status = "Prologue — tropes / likes / setup";
        else
            _status = "Menu / loading — start a round";

        _view = OverlayPresenter.Build(_live, _db, _status);
    }

    private void TickPickHint()
    {
        if (_db == null || _pickHint == null)
            return;
        PickHintToast.Tick(_db, _bridge, _pickHint.Value);
    }

    private void TickSecretAlerts()
    {
        if (_db == null || _secretAlerts == null)
            return;
        if (Time.unscaledTime < _nextSecretCheck)
            return;
        _nextSecretCheck = Time.unscaledTime + 0.25f;
        SecretEndingAlerts.Tick(_db, _bridge, _secretAlerts.Value, _secretAlertsFirstOnly.Value);
    }
}
