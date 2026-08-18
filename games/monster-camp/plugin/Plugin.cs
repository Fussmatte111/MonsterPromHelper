using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterCampHelper.Ingame;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    internal static Plugin Instance = null!;
    internal static Harmony Harmony = null!;

    internal static bool OverlayOpen;
    internal static int LastToggleFrame = -1;

    private static bool _bootstrapped;

    private EventDb _db = null!;
    private DrinkDb _drinks = null!;
    internal DrinkDb Drinks => _drinks;
    private readonly GameBridge _bridge = new GameBridge();
    private ConfigEntry<KeyCode> _toggleKey = null!;
    private ConfigEntry<KeyCode> _toggleKeyAlt = null!;
    private ConfigEntry<bool> _secretAlerts = null!;
    private ConfigEntry<bool> _secretAlertsFirstOnly = null!;
    private ConfigEntry<bool> _pickHint = null!;
    private ConfigEntry<bool> _drinkInfo = null!;

    private string _status = "";
    private int _lastToggleFrame = -1;
    private bool _loggedImguiDraw;
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
            _drinks = new DrinkDb();
            _drinks.LoadFromFolder(dataDir);

            Log.LogInfo("Plugin path: " + pluginDir);
            Log.LogInfo(
                "Data: "
                + dataDir
                + " ("
                + _db.EventCount
                + " Events, "
                + _drinks.DrinkCount
                + " Drinks)");

            _toggleKey = Config.Bind("Overlay", "ToggleKey", KeyCode.F8, "Toggle overlay");
            _toggleKeyAlt = Config.Bind("Overlay", "ToggleKeyAlt", KeyCode.F9, "Toggle overlay (alternate)");
            _secretAlerts = Config.Bind("Alerts", "SecretEndingToast", true, "Secret ending toast");
            _secretAlertsFirstOnly = Config.Bind("Alerts", "SecretEndingFirstOnly", true, "Secret toast only once per route");
            _pickHint = Config.Bind("Alerts", "PickHint", true, "Pick hint during events (without F8)");
            _drinkInfo = Config.Bind("Alerts", "DrinkInfo", true, "Ctrl+click a drink to show its effect");

            if (_toggleKey.Value == _toggleKeyAlt.Value)
                _toggleKeyAlt.Value = KeyCode.F9;

            _status = "F8 = Overlay | Ctrl+click = drink info";
            _hudUntil = Time.unscaledTime + 30f;
            OverlayOpen = false;

            OverlayHost.EnsureExists();
            PluginTickPatch.TryApply();
            DrinkHoverPatch.TryApply();

            NguiInputBlockPatch.TryApply();
            OverlayCursorPatch.TryApply();
            NguiOverlayPatch.TryApply();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Log.LogInfo(
                "Helper v"
                + PluginInfo.PLUGIN_VERSION
                + " — Pick hint: "
                + _pickHint.Value
                + ", Drink-Info: "
                + _drinkInfo.Value);
        }
        catch (Exception ex)
        {
            Log.LogError("Awake failed: " + ex);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NguiOverlayView.ResetForNewScene();
        SecretEndingAlerts.ResetScene();
        PickHintToast.ResetScene();
        DrinkInfoHelper.ResetScene();

        if (OverlayOpen)
        {
            NguiOverlayView.Hide();
            RefreshOverlayData();
        }
    }

    internal void TickFrame()
    {
        if (Instance != this)
            return;
        if (_tickFrameGuard == Time.frameCount)
            return;
        _tickFrameGuard = Time.frameCount;

        PollToggleKeyDown();
        NguiInputGate.Sync();
        GameInputLock.Sync();
        TickSecretAlerts();
        TickPickHint();
        TickDrinkInfo();

        if (!OverlayOpen)
            return;

        OverlayCursor.EnsureVisible();
        OverlayGui.GetPanelRect(out var px, out var py, out var pw, out var ph);
        OverlayGui.ApplyScrollWheel(px, py, ph);

        if (Time.unscaledTime < _nextPoll)
            return;
        _nextPoll = Time.unscaledTime + 0.15f;
        RefreshOverlayData();
    }

    private void PollToggleKeyDown()
    {
        if (Time.unscaledTime < _toggleLockUntil)
            return;

        KeyCode? key = null;
        if (Input.GetKeyDown(_toggleKey.Value))
            key = _toggleKey.Value;
        else if (_toggleKeyAlt.Value != _toggleKey.Value && Input.GetKeyDown(_toggleKeyAlt.Value))
            key = _toggleKeyAlt.Value;

        if (key == null)
            return;
        if (_lastToggleFrame == Time.frameCount)
            return;

        _lastToggleFrame = Time.frameCount;
        _toggleLockUntil = Time.unscaledTime + 0.5f;
        ApplyToggle(key.Value);
    }

    private void ApplyToggle(KeyCode viaKey)
    {
        OverlayOpen = !OverlayOpen;
        Log.LogInfo(OverlayOpen ? "Overlay ON (" + viaKey + ")" : "Overlay OFF (" + viaKey + ")");

        if (OverlayOpen)
            OverlayCursor.EnsureVisible();

        if (!OverlayOpen)
        {
            NguiOverlayView.Hide();
            NguiInputGate.Restore();
            GameInputLock.Release();
            _loggedImguiDraw = false;
            return;
        }

        _hudUntil = -1f;
        _loggedImguiDraw = false;
        _nextPoll = 0;

        try
        {
            NguiOverlayView.Hide();
            RefreshOverlayData();
            RomanceStatsPanel.SyncDrafts(_live);
            Log.LogInfo("Overlay ON — scene=" + _view.SceneName + ", DB=" + _db.EventCount + " Events");
        }
        catch (Exception ex)
        {
            Log.LogError("Overlay ON error: " + ex);
        }
    }

    internal void RefreshLiveState(bool syncEditorDrafts)
    {
        RefreshOverlayData();
        if (OverlayOpen && syncEditorDrafts)
            RomanceStatsPanel.SyncDrafts(_live);
    }

    private void RefreshOverlayData()
    {
        if (_db == null)
            return;

        if (!_bridge.TryRead(out _live, out var err))
            _status = err;
        else if (_live.EventActive)
            _status = "Event active — pick below";
        else if (_live.InSchool)
            _status = "Camp round — waiting for dialog";
        else
            _status = "Menu / loading — start a round";

        if (MonoUtil.HasText(err) && _live.InSchool && string.IsNullOrEmpty(_status))
            _status = err;

        _view = OverlayPresenter.Build(_live, _db, _status);
    }

    private void TickPickHint()
    {
        if (_db == null || _pickHint == null)
            return;
        PickHintToast.Tick(_db, _bridge, _pickHint.Value);
    }

    private void TickDrinkInfo()
    {
        if (_drinks == null || _drinkInfo == null)
            return;
        DrinkInfoHelper.Tick(_drinks, _drinkInfo.Value);
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

    internal void DrawOverlayGui()
    {
        if (_db == null || Instance != this)
            return;

        PickHintToast.Draw();
        DrinkInfoHelper.Draw();
        SecretEndingAlerts.DrawToast();

        if (!OverlayOpen)
        {
            OverlayGui.DrawHud(_hudUntil, false);
            return;
        }

        if (_view.DbEventCount == 0 && _db.EventCount > 0)
            _view = OverlayPresenter.Build(_live, _db, _status);

        NguiOverlayView.Hide();

        try
        {
            OverlayGui.DrawFull(_view, _live, _bridge);
            if (!_loggedImguiDraw)
            {
                _loggedImguiDraw = true;
                Log.LogInfo("Overlay drawn (IMGUI).");
            }
        }
        catch (Exception ex)
        {
            Log.LogError("IMGUI overlay: " + ex);
        }
    }

    internal void RenderOverlay()
    {
        DrawOverlayGui();
    }
}
