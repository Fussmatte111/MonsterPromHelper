# Monster Prom Helper — BepInEx Plugins

Ingame-Hilfsmods für die **Monster-Prom-Reihe** (Beautiful Glitch). Die Plugins lesen Dialoge, Stats und Events **direkt aus dem Spiel** — ohne OCR und ohne manuelles RAM-Raten.

> **Unofficial fan project.** Nicht von Beautiful Glitch. Nutzung auf eigenes Risiko; Mods können Achievements oder Online-Features beeinflussen.

Repository: [github.com/Fussmatte111/MonsterPromHelper](https://github.com/Fussmatte111/MonsterPromHelper)

Übersicht über das gesamte Repo (Python Log-Watcher + Overlay): [**README.md**](../README.md) im Root.

---

## Unterstützte Spiele

| Spiel | Plugin-Ordner | Build-Skript | Standard-EXE |
|-------|----------------|--------------|--------------|
| **Monster Prom** (1) | `MonsterPromHelper` | `build.ps1` | `MonsterProm.exe` |
| **Monster Prom 2: Monster Camp** | `MonsterCampHelper` | `build-camp.ps1` | `MonsterCamp.exe` |
| **Monster Prom 4: Monster Con** | `MonsterProm4Helper` | `build-mp4.ps1` | `MonsterCon.exe` |

Jedes Spiel braucht **eigenes BepInEx** im jeweiligen Installationsordner. Die Plugins sind **nicht** untereinander austauschbar.

---

## Features

| Feature | Beschreibung |
|---------|--------------|
| **Overlay (F8 / F9)** | Event-Name, Antwort-Texte, Stat-Empfehlung, optional Stats-/Love-Editor |
| **Pick-Hint** | Kleines Popup während Dialogen: empfohlene Option nach deinen Stats (ohne Overlay) |
| **Secret-Toasts** | Hinweis, wenn ein Secret-Ending-Event erkannt wird |
| **Event-Datenbank** | Abgleich per Antwort-Texte + interne Event-Namen (`events_db.json`) |
| **Drink-Info** *(nur Camp)* | **Strg + Linksklick** auf Drink → Effekt aus `drinks_db.json` |

---

## Voraussetzungen

- **Windows**, Spiel über Steam
- **BepInEx 5.4** für Unity Mono — bei allen drei Spielen in der Praxis **32-bit (x86)**
- **.NET SDK** — nur wenn du die Plugins selbst kompilierst ([dotnet.microsoft.com](https://dotnet.microsoft.com/download))

### BepInEx korrekt installiert?

Nach dem **ersten Spielstart** sollten existieren:

- `BepInEx/LogOutput.log`
- `BepInEx/config/`
- `BepInEx/plugins/`

Wenn nur `BepInEx/core/` da ist und kein Log entsteht → meist **falsches BepInEx (x64 statt x86)** oder blockierte `winhttp.dll`.

---

## Schnellinstallation

### Option A — Fertiges Paket (ohne Build)

1. [BepInEx x86](https://github.com/BepInEx/BepInEx/releases) in den **Spielordner** entpacken.
2. Spiel einmal starten und wieder schließen.
3. Inhalt des passenden `install-pack-*`-Ordners in denselben Spielordner kopieren (Ordner mergen):

| Spiel | Paket |
|-------|--------|
| Monster Prom | `install-pack/` |
| Monster Camp | `install-pack-camp/` |
| Monster Con | `install-pack-mp4/` |

4. Spiel starten. In `BepInEx/LogOutput.log` sollte z. B. stehen: `Helper v… — Plugin loaded`.

### Option B — Automatisch (nur Monster Prom 1)

PowerShell **als Administrator** (bei Installation unter `Program Files`):

```powershell
cd path\to\MonsterPromHelper\bepinex-plugin
.\install-bepinex.ps1
```

Lädt BepInEx, erkennt 32/64-bit und deployt das MP1-Plugin.

### Option C — Aus Quellcode bauen

```powershell
cd path\to\MonsterPromHelper\bepinex-plugin

# Monster Prom 1
.\build.ps1

# Monster Camp
.\build-camp.ps1

# Monster Prom 4
.\build-mp4.ps1
```

Anderen Spielordner angeben:

```powershell
$env:MONSTER_PROM_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom"
.\build.ps1

$env:MONSTER_CAMP_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom 2 - Monster Camp"
.\build-camp.ps1

$env:MONSTER_CON_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom 4 - Monster Con"
.\build-mp4.ps1
```

Danach den Inhalt von `install-pack*` in den Spielordner kopieren.

**Build-Fehler?** BepInEx muss im Spielordner liegen (`BepInEx/core/BepInEx.dll`), oder das Skript lädt BepInEx nach `lib/` für den Compiler.

---

## Steuerung & Konfiguration

| Taste (Standard) | Aktion |
|------------------|--------|
| **F8** | Overlay ein / aus |
| **F9** | Overlay ein / aus (Alternative) |
| **Strg + Linksklick** *(Camp)* | Drink-Effekt anzeigen |

Config-Dateien (nach erstem Start unter `BepInEx/config/`):

| Spiel | Config |
|-------|--------|
| Monster Prom | `com.monsterprom.helper.ingame.cfg` |
| Monster Camp | `com.monstercamp.helper.ingame.cfg` |
| Monster Con | `com.monsterprom4.helper.ingame.cfg` |

Wichtige Optionen:

```ini
[Overlay]
ToggleKey = F8
ToggleKeyAlt = F9

[Alerts]
PickHint = true
SecretEndingToast = true
SecretEndingFirstOnly = true
DrinkInfo = true          ; nur Monster Camp
```

---

## Pro Spiel

### Monster Prom (1) — v1.7.2

- **Szene:** `InGame_School`
- **Mechanik:** Zwei Stats pro Event; Spiel wählt die höhere Stat-Option
- **Daten:** ~430 Events in `data/events_db.json`, Secret-Routen in `secret_endings.json`
- **LIs:** Damien, Liam, Miranda, Polly, Scott, Vera, Calculester, Zoe
- **Stats:** SMARTS, BOLD, CREATIVE, CHARM, FUN, MONEY

```
Monster Prom/
  BepInEx/plugins/MonsterPromHelper/
    MonsterPromHelper.Ingame.dll
    data/events_db.json
    data/secret_endings.json
```

### Monster Prom 2: Monster Camp — v1.0.1

- **Szene:** `MainGame`
- **Mechanik:** wie MP1 (Stat-Vergleich pro Option)
- **Daten:** [Community-Spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit) → ~270 Events, 63 Drinks, Secret-Events
- **LIs:** Damien, Calculester, Milo, Dahlia, Joy, Aaravi
- **Extra:** Drink-Infos per Strg+Klick in der Drink-Auswahl

```
Monster Prom 2 - Monster Camp/
  BepInEx/plugins/MonsterCampHelper/
    MonsterCampHelper.Ingame.dll
    data/events_db.json
    data/drinks_db.json
    data/secret_endings.json
```

Datenbank beim Build aktualisieren: `python tools/build_camp_db.py` (Internet nötig für Google Sheet).

### Monster Prom 4: Monster Con — v1.3.1

- **Szene:** `MainGame`, Prolog unterstützt
- **Mechanik:** **Stat-Austausch** (+STAT / −STAT pro Option, nicht reiner Höchstwert)
- **Daten:** Events aus [Steam Event Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3470379401), Pregame aus [Wiki](https://monsterprom.wiki.gg/wiki/Pre-Game_Preparation/Monster_Con)
- **LIs:** Liam, Zoe, Omen, Doug, Nico, April
- **Extra:** Badges an Choice-Boxen im Dialog; NGUI-Overlay

```
Monster Prom 4 - Monster Con/
  BepInEx/plugins/MonsterProm4Helper/
    MonsterProm4Helper.Ingame.dll
    data/events_db.json
    data/pregame_db.json
    data/secret_endings.json
```

---

## Vergleich der Plugins

| | MP1 | Monster Camp | MP4 (Con) |
|--|-----|--------------|-----------|
| GUID | `com.monsterprom.helper.ingame` | `com.monstercamp.helper.ingame` | `com.monsterprom4.helper.ingame` |
| Stat-Logik | Höherer Stat gewinnt | Höherer Stat gewinnt | +/- Stat-Austausch |
| MONEY-Stat | Ja | Nein | Nein |
| Drink-Info | — | Ja | — |
| Pregame-Hints | — | — | Ja |

---

## Ordnerstruktur

```
bepinex-plugin/
  README.md
  build.ps1 / build-camp.ps1 / build-mp4.ps1
  install-bepinex.ps1          # MP1 Auto-Install
  fix-plugin-path.ps1          # Doppelte Plugin-Pfade bereinigen
  fix-double-plugin.ps1        # Zwei DLL-Instanzen entfernen
  MonsterPromHelper.Ingame/    # MP1 Quellcode
  MonsterCampHelper.Ingame/    # Camp Quellcode
  MonsterProm4Helper.Ingame/   # MP4 Quellcode
  install-pack/                # MP1 Deploy-Paket
  install-pack-camp/
  install-pack-mp4/
  lib/                         # BepInEx.dll für Build (optional)
```

Event-Daten liegen im Repo unter `data/`, `data-camp/` und `data-mp4/` und werden beim Build in die Install-Pakete kopiert.

---

## Fehlerbehebung

| Problem | Lösung |
|---------|--------|
| Kein `LogOutput.log` | BepInEx **x86** installieren; `winhttp.dll` neben der `.exe` |
| Plugin lädt nicht | DLL direkt unter `BepInEx/plugins/<PluginName>/`, nicht doppelt verschachtelt → `fix-plugin-path.ps1` |
| Overlay flackert AN/AUS | Zwei Plugin-Kopien → `fix-double-plugin.ps1`, Spiel neu starten |
| „0 Events“ / leeres Overlay | `data/events_db.json` fehlt → Build erneut oder `data/` kopieren |
| Pick-Hint erscheint nicht | In aktiver Runde spielen; Dialog mit zwei sichtbaren Antworten; `Alerts.PickHint = true` |
| Camp: Drink-Info fehlt | Log: `Drink-Hooks aktiv (2).` — sonst v1.0.1+ installieren |
| Build: Assembly-CSharp fehlt | Umgebungsvariable `MONSTER_*_DIR` auf installiertes Spiel setzen |
| Achievements | Mods können Steam-Achievements beeinflussen — Offline/Singleplayer üblich |

Logs prüfen: `BepInEx/LogOutput.log` und `%LOCALAPPDATA%/../LocalLow/Beautiful Glitch/<Spielname>/Player.log`.

---

## Python-Hintergrund-Agent (optional)

Zusätzlich gibt es im Repo-Root einen **Log-Watcher** mit Windows-Toasts und ein **Desktop-Overlay** mit OCR:

- `start.bat` / `start.ps1` — Toasts aus dem Unity-Log
- `start_overlay.bat` — separates Overlay-Fenster

BepInEx und Python können parallel laufen. Details: [**README.md**](../README.md)

---

## Entwicklung

- **Sprache:** C# (.NET Framework 4.7.2), Harmony, Unity IMGUI / NGUI
- **Runtime:** Unity Mono (CLR 2.0) — kein modernes C# in Hot Paths
- **Reflection:** Spiel-APIs sind teils non-public; `GameBridge` liest Runtime-State

Pull Requests und Issues willkommen: [github.com/Fussmatte111/MonsterPromHelper/issues](https://github.com/Fussmatte111/MonsterPromHelper/issues)

---

## Danksagungen

- Event-Daten: Community-Guides, [Monster Prom Wiki](https://monsterprom.wiki.gg/), [Camp Spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit)
- [BepInEx](https://github.com/BepInEx/BepInEx) — Modding-Framework
