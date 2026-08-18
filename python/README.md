# Python tools

Optional **log watcher** (Windows toasts) and **desktop overlay** (stats, event search, OCR). The in-game BepInEx plugins are more reliable for live picks.

Requires **Windows 10/11** and **Python 3.10+**. Launch the game at least once so the Unity log exists.

## Log watcher (toasts)

From the repo root:

```powershell
.\start.ps1
```

Or from this folder: `.\start.ps1` / `start.bat`.

First run copies `config.example.json` to `config.json` and installs `requirements.txt`.

Scan MPLogs after a run:

```powershell
.\start.ps1 --scan-mplogs
```

Set `"game"` in `config.json` to `Monster Prom` or `Monster Camp`.

### Notifications

| Option | Default | What |
|--------|---------|------|
| `secret_ending` | on | Secret-ending events from that game's `secret_endings.json` |
| `secret_ending_first_only` | on | Each secret hit once per session |
| `achievement` | on | Steam achievements |
| `game_end` | on | `==== GAME END! ====` |
| `interest_lock` | on | Route lock (`INTEREST LOCK: …`) |
| `plotline` | off | Plotline hints |
| `event_outcome` | off | Every event (can be noisy) |

Fewer toasts in menus:

- **`skip_history_on_start`** — ignore old log lines on start
- **`only_notify_in_round`** — only during the active school/camp week

Logs are auto-detected under `%LOCALAPPDATA%\..\LocalLow\Beautiful Glitch\<game>\` (`output_log.txt` or `Player.log`). MPLogs under `Steam\steamapps\common\<game>\MPLogs`. Override paths in `config.json` if Steam is elsewhere.

## Desktop overlay (OCR / search)

```powershell
.\start_overlay.bat
```

Separate window: enter stats, search events, optional screenshot OCR. The game window must be **visible** (not minimized). If RAM reads fail, run **as Administrator**.

Tips:

1. Enter **stats** (optionally calibrate for live RAM values).
2. **Screenshot → event** when both choice buttons are visible → OCR → event DB match.
3. **Find dialog:** type words from the dialog (not internal names like `DamienMatchingTattoos`).
4. **★** = your highest matching stat (usually the successful option in MP1).

Disable OCR: `"overlay": { "dialog_ocr": false }` in `config.json`.

OCR language is English (`en`). Install the Windows OCR pack: Settings → Language → English (United States) → OCR.

Event data comes from `games/<game>/data/` based on `"game"` in `config.json`.

## Limits

- Before you pick an answer, the event name is often not in the log yet — BepInEx is more reliable.
- Automatic RAM scan is experimental and limited by default.
- `output_log.txt` is often reset on game launch — the watcher restarts from the new file.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| Log not found | Launch the game once; check `game` in `config.json` |
| No toasts | Allow Windows notifications; check `notifications` in config |
| Overlay / OCR stuck | Restart the overlay; both choices must be visible |
| Spam in the main menu | `only_notify_in_round: true`, `skip_history_on_start: true` |
