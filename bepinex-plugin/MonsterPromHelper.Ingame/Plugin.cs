using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonsterPromHelper.Ingame;

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
    private readonly GameBridge _bridge = new GameBridge();
    private ConfigEntry<KeyCode> _toggleKey = null!;
    private ConfigEntry<KeyCode> _toggleKeyAlt = null!;
    private ConfigEntry<bool> _secretAlerts = null!;
    private ConfigEntry<bool> _secretAlertsFirstOnly = null!;
    private ConfigEntry<bool> _pickHint = null!;

    private string _status = "";
    private int _lastToggleFrame = -1;
    private bool _loggedImguiDraw;
    private LiveGameState _live = new LiveGameState();
    private OverlayViewModel _view = new OverlayViewModel();
    private float _nextPoll;
    private float _hudUntil;

    private float _toggleLockUntil;
    private object? _uiCameraType;
    private float _nextUiHookTry;
    private float _nextSecretCheck;
    private void Awake()
    {
        if (_bootstrapped && Instance != null && Instance != this)
        {
            Log.LogWarning("Zweites Plugin-Exemplar entfernt (verursacht AN+AUS-Bug).");
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
            Log.LogInfo($"Plugin-Pfad: {pluginDir}");
            Log.LogInfo($"Daten: {dataDir} ({_db.EventCount} Events)");

            _toggleKey = Config.Bind("Overlay", "ToggleKey", KeyCode.F8, "Overlay ein/aus");
            _toggleKeyAlt = Config.Bind("Overlay", "ToggleKeyAlt", KeyCode.F9, "Overlay ein/aus (Alternativ, nicht Insert)");
            _secretAlerts = Config.Bind("Alerts", "SecretEndingToast", true,
                "In-Game-Meldung wenn Secret-Ending-Dialog/Antwort erkannt wird (Wiki-Liste)");
            _secretAlertsFirstOnly = Config.Bind("Alerts", "SecretEndingFirstOnly", true,
                "Jeden Secret-Treffer nur einmal pro Spielsitzung melden");
            _pickHint = Config.Bind("Alerts", "PickHint", true,
                "Kleines Popup bei Dialogen mit empfohlener Option (ohne F8)");

            if (_toggleKey.Value == _toggleKeyAlt.Value)
            {
                _toggleKeyAlt.Value = KeyCode.F9;
                Log.LogWarning("ToggleKey und ToggleKeyAlt waren gleich — Alt auf F9 gesetzt.");
            }

            if (_toggleKeyAlt.Value == KeyCode.Insert || _toggleKey.Value == KeyCode.Insert)
            {
                if (_toggleKey.Value == KeyCode.Insert)
                    _toggleKey.Value = KeyCode.F8;
                if (_toggleKeyAlt.Value == KeyCode.Insert)
                    _toggleKeyAlt.Value = KeyCode.F9;
                Log.LogWarning("Insert entfernt (doppeltes AN/AUS). Nutze F8 oder F9.");
            }

            _status = "F8 = Overlay (F9 alternativ)";
            _hudUntil = Time.unscaledTime + 25f;
            OverlayOpen = false;

            OverlayHost.EnsureExists();

            _uiCameraType = MonoUtil.FindGameType("UICamera");
            NguiInputBlockPatch.TryApply();
            OverlayCursorPatch.TryApply();
            NguiOverlayPatch.TryApply();
            SceneManager.sceneLoaded += OnSceneLoaded;

            Log.LogInfo($"Helper v{PluginInfo.PLUGIN_VERSION} — Overlay F8, Secret-Toasts: {_secretAlerts.Value}");
            Log.LogInfo($"Secret-Hints geladen: {_db.SecretHintCount}");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Awake fehlgeschlagen: {ex}");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NguiOverlayView.ResetForNewScene();
        SecretEndingAlerts.ResetScene();
        PickHintToast.ResetScene();

        if (MonoUtil.IsNull(_uiCameraType))
            _uiCameraType = MonoUtil.FindGameType("UICamera");

        if (OverlayOpen)
        {
            NguiOverlayView.Hide();
            RefreshOverlayData();
        }
    }

    private void Update()
    {
        if (Instance != this)
            return;

        PollToggleKeyDown();
        TryAttachUiHooks();
        NguiInputGate.Sync();
        GameInputLock.Sync();
        TickSecretAlerts();
        TickPickHint();

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
        Log.LogInfo(OverlayOpen ? $"Overlay AN ({viaKey})" : $"Overlay AUS ({viaKey})");

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
            Log.LogInfo($"Overlay IMGUI — Szene={_view.SceneName}, DB={_db.EventCount} Events");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Overlay AN Fehler: {ex}");
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
            _status = "Event aktiv — Empfehlung unten";
        else if (_live.InSchool)
            _status = "In der Runde — warte auf Dialog";
        else
            _status = "Menü / Lade — Runde starten";

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

    private void TickSecretAlerts()
    {
        if (_db == null || _secretAlerts == null)
            return;
        if (Time.unscaledTime < _nextSecretCheck)
            return;
        _nextSecretCheck = Time.unscaledTime + 0.25f;
        SecretEndingAlerts.Tick(_db, _bridge, _secretAlerts.Value, _secretAlertsFirstOnly.Value);
    }

    private void TryAttachUiHooks()
    {
        // Overlay zeichnet nur noch ueber OverlayHost (ein OnGUI-Pfad).
    }

    internal void DrawOverlayGui()
    {
        if (_db == null || Instance != this)
            return;

        PickHintToast.Draw();
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
                Log.LogInfo("Overlay gezeichnet (IMGUI).");
            }
        }
        catch (System.Exception ex)
        {
            Log.LogError($"IMGUI-Overlay: {ex}");
        }
    }

    internal void RenderOverlay()
    {
        DrawOverlayGui();
    }
}
