# Monster Prom Helper

Hintergrund-Agent für **Monster Prom** (auch Camp / Prom 2 konfigurierbar). Liest das Live-Unity-Log und warnt dich per Windows-Toast, wenn z.B. ein **Secret-Ending-Event** getriggert wird.

## Was wird getrackt?

| Quelle | Wann | Beispiel |
|--------|------|----------|
| `output_log.txt` | **Live** während du spielst | `- Output chosen: Option2Success for 0392: Coke1` |
| `MPLogs/` | Nach Spielende (optional `--scan-mplogs`) | Event-Liste der letzten Runde |

### Benachrichtigungen (in `config.json`)

- **secret_ending** — Events aus der Secret-Ending-Liste (56 Events, alle Routen)
- **achievement** — Steam-Achievements
- **game_end** — `==== GAME END! ====`
- **interest_lock** — Route-Lock (`INTEREST LOCK: Yellow -> SCOTT`)
- **plotline** — Plotline-Hinweise
- **event_outcome** — jedes Event (standardmäßig aus)

## Ingame-Overlay (BepInEx, empfohlen)

Zuverlässiger Dialog-Helfer **im Spiel** mit **Insert** — siehe Ordner [`bepinex-plugin/README.md`](bepinex-plugin/README.md) (Installation BepInEx + Plugin bauen/kopieren).

## Dialog-Helfer (Python-Overlay)

```powershell
.\start_overlay.bat
```

**Voraussetzungen:** Monster Prom läuft. Bei Memory-Problemen: **als Administrator** starten.

### Ablauf (vor dem Klick!)

1. **Stats** eintragen (und optional kalibrieren für Live-Stats / RAM-Scan).
2. **Screenshot → Event:** Button im Overlay, wenn **beide Antworten** im Spiel sichtbar sind. Screenshot vom Fenster → OCR der zwei Optionen → Abgleich mit der Event-DB.
3. **Auto-OCR** (optional): alle ~2,5 s automatisch im Dialog-Bereich.
4. Monster Prom muss **sichtbar** sein (nicht minimiert). Overlay einmal neu starten nach `pip install` (siehe `start_overlay.bat`).
5. Bei unsicherem OCR: **Treffer-Liste** prüfen oder kurz Wörter unter **„Dialog finden“** eintippen.
6. **Auto-RAM** (wenn kalibriert) ergänzt Fungus `currEvent` + Text im Speicher.
7. **RAM aktualisieren:** Button erzwingt einen neuen RAM-Scan (auch nach manueller Suche/Screenshot).
8. **Blockliste leeren:** Setzt ignorierte/fertige Events zurück, wenn RAM nur „blockiert: …“ meldet.

**Wenn Screenshot/RAM hängen:** Overlay neu starten. Screenshot braucht **beide Antworten sichtbar**; RAM braucht **Kalibrieren**. Bei mehreren RAM-Kandidaten → **Treffer-Liste**. `event_scan_seconds` nicht unter 0,4 (sonst blockiert Auto-Scan die Buttons).

**Wichtig:** Im Spiel steht nie `DamienMatchingTattoos` — das sind interne Namen. Nach dem Klick steht der Name im Log (`Fertig: …`), zum Nachschauen.

**OCR abschalten:** In `config.json` → `"overlay": { "dialog_ocr": false }`.

**OCR-Sprache:** Standard Englisch (`en`). Bei fehlendem OCR-Paket in Windows: Einstellungen → Zeit und Sprache → Sprache → Englisch (USA) → optional „Handschrift“/OCR-Komponente.

**★** = höchster Stat-Wert bei dir (meist Erfolg).

Datenbank: 400+ Events (`data/events_db.json`).

## Schnellstart (Background-Toasts)

1. **Python 3.10+** installiert?
2. Doppelklick auf `start.bat` **oder** in PowerShell:

```powershell
cd "C:\Users\matti\Downloads\MonsterPromHelper"
.\start.ps1
```

3. Monster Prom starten und spielen — der Agent läuft im Terminal im Hintergrund.

Beim ersten Start wird `config.json` aus `config.example.json` erzeugt.

### Keine Spam-Toasts im Menü?

Standardmäßig:

- **`skip_history_on_start`** — beim Start werden alte Log-Zeilen übersprungen (nur was *danach* ins Log kommt)
- **`only_notify_in_round`** — Toasts nur in `InGame_School` (aktive Woche), nicht im Hauptmenü/Mod-Tool

## Pfade

Standard (automatisch):

- Log: `%LOCALAPPDATA%\..\LocalLow\Beautiful Glitch\Monster Prom\output_log.txt`
- MPLogs: `Steam\steamapps\common\Monster Prom\MPLogs`

Manuell in `config.json`:

```json
{
  "output_log_path": "C:\\Users\\...\\output_log.txt",
  "mplogs_path": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Monster Prom\\MPLogs"
}
```

## Secret Endings

Die Event-Namen stammen aus der [Steam Event-Liste](https://steamcommunity.com/sharedfiles/filedetails/?id=2043551842) und liegen in `data/secret_endings.json`. Eigene Events kannst du dort ergänzen.

**Hinweis:** Das Spiel schreibt Event-Namen case-sensitive (`Coke1` vs `coke2`) — der Helper matcht case-insensitive.

## MPLogs nach Runde scannen

```powershell
.\start.ps1 --scan-mplogs
```

## Autostart (optional)

Task Scheduler → neue Aufgabe → bei Anmeldung → Programm: `python` → Argumente: `-m src.main` → Start in: dieser Ordner.

## Grenzen

- **Vor der Wahl** kennt das Spiel den Event-Namen nicht im Log — deshalb **Event manuell**.
- Automatischer RAM-Scan ist unzuverlässig und deshalb deaktiviert.
- `output_log.txt` wird beim Spielstart oft geleert/neu geschrieben — der Watcher setzt sich automatisch zurück.
