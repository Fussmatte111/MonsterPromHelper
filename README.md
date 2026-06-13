# Monster Prom Helper

Hilfstools für die **Monster-Prom-Reihe** (Beautiful Glitch): Ingame-Overlays per **BepInEx** und optional ein **Python-Log-Agent** mit Windows-Toasts.

> **Unofficial fan project.** Nicht von Beautiful Glitch. Nutzung auf eigenes Risiko; Mods können Achievements oder Online-Features beeinflussen.

Repository: [github.com/Fussmatte111/MonsterPromHelper](https://github.com/Fussmatte111/MonsterPromHelper)

---

## Zwei Komponenten

| | **BepInEx-Plugins** *(empfohlen)* | **Python-Tools** *(optional)* |
|--|-----------------------------------|-------------------------------|
| **Was** | Ingame-Overlay, Pick-Hints, Secret-Alerts | Log-Watcher + Desktop-Overlay |
| **Datenquelle** | Direkt aus dem Spiel (Stats, Dialog-UI) | Unity-Log, optional OCR/RAM |
| **Doku** | [`bepinex-plugin/README.md`](bepinex-plugin/README.md) | Diese Datei |
| **Start** | Plugin ins Spiel kopieren | `start.bat` / `start_overlay.bat` |

Beides kann **parallel** laufen: BepInEx für präzise Empfehlungen im Spiel, Python für Toasts im Hintergrund.

---

## Unterstützte Spiele

| Spiel | BepInEx-Plugin | Python (`config.json`) |
|-------|----------------|-------------------------|
| **Monster Prom** (1) | `MonsterPromHelper` | `"game": "Monster Prom"` |
| **Monster Prom 2: Monster Camp** | `MonsterCampHelper` | `"game": "Monster Camp"` |
| **Monster Prom 4: Monster Con** | `MonsterProm4Helper` | *(primär BepInEx)* |

Steam-Ordner (Windows, typisch):

| Spiel | Ordner unter `steamapps/common/` |
|-------|----------------------------------|
| Monster Prom | `Monster Prom/` |
| Monster Camp | `Monster Prom 2 - Monster Camp/` |
| Monster Con | `Monster Prom 4 - Monster Con/` |

---

## Schnellstart — BepInEx *(empfohlen)*

1. [BepInEx 5.4 x86](https://github.com/BepInEx/BepInEx/releases) in den Spielordner entpacken.
2. Spiel einmal starten (damit `BepInEx/plugins/` angelegt wird).
3. Passendes Paket aus `bepinex-plugin/install-pack*` in denselben Ordner kopieren.
4. **F8** im Spiel → Overlay mit Event-Empfehlung.

Ausführliche Anleitung, Build-Skripte und Fehlerbehebung:

**→ [`bepinex-plugin/README.md`](bepinex-plugin/README.md)**

---

## Schnellstart — Python

### Voraussetzungen

- **Windows 10/11**
- **Python 3.10+**
- Spiel mindestens einmal gestartet (Log-Datei wird angelegt)

### Log-Watcher (Toasts)

```powershell
cd path\to\MonsterPromHelper
.\start.ps1
```

Oder Doppelklick auf `start.bat`. Beim ersten Start wird `config.json` aus `config.example.json` erzeugt.

Das Skript installiert Abhängigkeiten (`pip install -r requirements.txt`) und überwacht das Unity-Log.

### Desktop-Overlay (OCR / Suche)

```powershell
.\start_overlay.bat
```

Separates Fenster mit Stat-Eingabe, Event-Suche und optional Screenshot-OCR. Monster Prom muss **sichtbar** sein (nicht minimiert). Bei Speicher-Problemen: **als Administrator** starten.

---

## Python — Was wird getrackt?

| Quelle | Wann | Beispiel |
|--------|------|----------|
| `output_log.txt` / `Player.log` | Live während du spielst | `Output chosen: Option2Success for …` |
| `MPLogs/` | Nach der Runde (`--scan-mplogs`) | Event-Liste der letzten Session |

### Benachrichtigungen (`config.json`)

| Option | Standard | Beschreibung |
|--------|----------|--------------|
| `secret_ending` | an | Secret-Ending-Events aus `data/secret_endings.json` |
| `secret_ending_first_only` | an | Jeden Secret-Treffer nur einmal pro Session |
| `achievement` | an | Steam-Achievements |
| `game_end` | an | `==== GAME END! ====` |
| `interest_lock` | an | Route-Lock (`INTEREST LOCK: …`) |
| `plotline` | aus | Plotline-Hinweise |
| `event_outcome` | aus | Jedes Event (kann spammy sein) |

### Weniger Toasts im Menü

- **`skip_history_on_start`** — alte Log-Zeilen beim Start ignorieren
- **`only_notify_in_round`** — nur während der aktiven Schul-/Camp-Woche

---

## Python — Overlay-Tipps

1. **Stats** eintragen (optional kalibrieren für Live-Werte aus dem RAM).
2. **Screenshot → Event:** wenn beide Antwort-Buttons sichtbar sind → OCR → Abgleich mit der Event-DB.
3. **Dialog finden:** Stichwörter aus dem Dialog eintippen (nicht interne Event-Namen wie `DamienMatchingTattoos`).
4. **★** = höchste Stat bei dir (meist die erfolgreiche Option bei MP1-Mechanik).

OCR abschalten: `"overlay": { "dialog_ocr": false }` in `config.json`.

OCR-Sprache: Englisch (`en`). Falls nötig: Windows → Sprache → Englisch (USA) → OCR-Komponente installieren.

Datenbank MP1: ~430 Events in `data/events_db.json`.

---

## Konfiguration

`config.example.json` kopieren nach `config.json` (passiert automatisch beim ersten Start).

```json
{
  "game": "Monster Prom",
  "output_log_path": null,
  "mplogs_path": null,
  "poll_interval_seconds": 0.2,
  "skip_history_on_start": true,
  "only_notify_in_round": true,
  "notifications": { "secret_ending": true }
}
```

Pfade werden automatisch erkannt unter:

- Log: `%LOCALAPPDATA%\..\LocalLow\Beautiful Glitch\<Spielname>\` (`output_log.txt` oder `Player.log`)
- MPLogs: `Steam\steamapps\common\<Spielname>\MPLogs`

Manuelle Pfade in `config.json` setzen, wenn Steam woanders installiert ist.

---

## MPLogs nach einer Runde

```powershell
.\start.ps1 --scan-mplogs
```

---

## Autostart (optional)

Windows Task Scheduler → bei Anmeldung → Programm: `python` → Argumente: `-m src.main` → Start in: Repo-Ordner.

---

## Projektstruktur

```
MonsterPromHelper/
  README.md                 ← diese Datei (Python + Übersicht)
  config.example.json
  requirements.txt
  start.bat / start.ps1     ← Log-Watcher
  start_overlay.bat         ← Python-Overlay
  src/                      ← Python-Quellcode
  data/                     ← Event-DB Monster Prom 1
  data-camp/                ← Event- + Drink-DB Monster Camp
  data-mp4/                 ← Event- + Pregame-DB Monster Con
  tools/                    ← Skripte zum DB-Build
  bepinex-plugin/           ← BepInEx-Plugins (C#)
    README.md               ← Installationsanleitung Plugins
```

---

## Event-Daten & Secret Endings

| Spiel | Daten im Repo | Quelle |
|-------|---------------|--------|
| Monster Prom | `data/events_db.json`, `secret_endings.json` | [Steam Event Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2043551842) |
| Monster Camp | `data-camp/` | [Community-Spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit) |
| Monster Con | `data-mp4/` | [Steam Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3470379401), [Wiki Pregame](https://monsterprom.wiki.gg/wiki/Pre-Game_Preparation/Monster_Con) |

Event-Namen im Log sind case-insensitive matchbar (`Coke1` / `coke2`).

DBs neu bauen:

```powershell
python tools/build_event_db.py      # MP1
python tools/build_camp_db.py       # Camp (Internet)
python tools/build_mp4_event_db.py  # MP4
python tools/build_mp4_pregame_wiki.py
```

---

## Grenzen (Python)

- **Vor der Antwort-Wahl** steht der Event-Name oft noch nicht im Log — Overlay/OCR oder BepInEx sind zuverlässiger.
- Automatischer RAM-Scan ist experimentell und standardmäßig eingeschränkt.
- `output_log.txt` wird beim Spielstart oft zurückgesetzt — der Watcher springt automatisch neu an.

Für zuverlässige **Live-Empfehlungen im Dialog** → **BepInEx-Plugins** nutzen.

---

## Fehlerbehebung (Python)

| Problem | Lösung |
|---------|--------|
| Log nicht gefunden | Spiel einmal starten; `game` in `config.json` prüfen |
| Keine Toasts | Windows-Benachrichtigungen erlauben; `notifications` in Config prüfen |
| Overlay/OCR hängt | Overlay neu starten; beide Antworten müssen sichtbar sein |
| Spam im Hauptmenü | `only_notify_in_round: true`, `skip_history_on_start: true` |

BepInEx-Probleme → [`bepinex-plugin/README.md`](bepinex-plugin/README.md#fehlerbehebung)

---

## Mitwirken

Issues und Pull Requests: [github.com/Fussmatte111/MonsterPromHelper/issues](https://github.com/Fussmatte111/MonsterPromHelper/issues)

---

## Danksagungen

- Beautiful Glitch — *Monster Prom*
- Community-Guides, [Monster Prom Wiki](https://monsterprom.wiki.gg/), Spreadsheet-Autoren
- [BepInEx](https://github.com/BepInEx/BepInEx)
