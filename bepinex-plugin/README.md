# Monster Prom Helper — BepInEx (Ingame-Overlay)

Ingame-Mod mit **Overlay** (Standard: **Insert**). Liest Event, beide Antworten und Stats **direkt aus dem Spiel** — ohne OCR und ohne RAM-Raten.

## Was das Plugin kann

- **Insert** — Overlay ein-/ausblenden (umkonfigurierbar in `BepInEx/config/com.monsterprom.helper.ingame.cfg`)
- Zeigt **aktuelles Event** (`EventManager`, exakter Name wie im Log)
- Zeigt **beide Antwort-Texte** aus dem UI
- **★ Empfehlung** aus Spiel-Logik (`StatRequired_Option1/2` vs. deine Stats)
- Zusätzlich **Datenbank-Empfehlung** aus `events_db.json` (Hints, Routen)
- Warnung bei **Secret-Ending-Events** (`secret_endings.json`)

## Voraussetzungen

- **Monster Prom** (Steam, Windows)
- **.NET SDK** zum Bauen des Plugins ([dotnet.microsoft.com](https://dotnet.microsoft.com/download)) — nur wenn du selbst kompilierst
- **BepInEx 5** für Unity Mono — bei Monster Prom (Steam) fast immer **32-bit**

## Wichtig: Nur `BepInEx/core` ist normal (vor dem ersten erfolgreichen Start)

Frisch entpackt enthält das BepInEx-ZIP oft **nur** den Ordner `BepInEx\core\` (plus `winhttp.dll` im Spielroot).  
**Erst wenn BepInEx beim Spielstart wirklich lädt**, erscheinen zusätzlich:

- `BepInEx\LogOutput.log`
- `BepInEx\config\`
- `BepInEx\cache\`
- `BepInEx\plugins\` (Mods kommen hier rein)

Bleibt nach dem Starten **nur** `core` und es gibt **kein** `LogOutput.log` → BepInEx wurde **nicht** geladen (häufig: **falsche Architektur x64 statt x86**).

## Installation (Schritt für Schritt)

### 1. BepInEx + Plugin automatisch (empfohlen)

PowerShell **als Administrator** (wegen Program Files):

```powershell
cd "C:\Users\matti\Downloads\MonsterPromHelper\bepinex-plugin"
.\install-bepinex.ps1
```

Das Skript erkennt **32- vs. 64-bit**, lädt das passende BepInEx-ZIP, kopiert alles ins Spiel und legt das Helper-Plugin unter `BepInEx\plugins\MonsterPromHelper\` ab.

### 1b. BepInEx manuell

1. Monster Prom liegt bei Steam meist hier:  
   `C:\Program Files (x86)\Steam\steamapps\common\Monster Prom\`  
   Start-EXE: **`MonsterProm.exe`** (32-bit).
2. **Nicht** `BepInEx_win_x64` — für Monster Prom (Steam):  
   **`BepInEx_win_x86_5.4.23.5.zip`** von [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases)
3. ZIP **komplett** in den Spielordner entpacken (`winhttp.dll`, `doorstop_config.ini`, `BepInEx\core\`).
4. **`MonsterProm.exe` starten** (einmal durchspielen bis Menü reicht), dann schließen.
5. Prüfen: `BepInEx\LogOutput.log` enthält z. B. `Overlay bereit — Insert oder F8`.
6. Plugin muss hier liegen (nicht doppelt verschachtelt):  
   `BepInEx\plugins\MonsterPromHelper\MonsterPromHelper.Ingame.dll`  
   Falsch: `BepInEx\plugins\BepInEx\plugins\...` → `.\fix-plugin-path.ps1`

> **Hinweis:** Mods kann Steam bei Achievements beeinflussen — üblich für Offline/Singleplayer, aber du entscheidest selbst.

### 2. Plugin bauen (oder fertiges Paket nutzen)

PowerShell im Repo:

```powershell
cd "C:\Users\matti\Downloads\MonsterPromHelper\bepinex-plugin"
.\build.ps1
```

Anderer Spielordner:

```powershell
$env:MONSTER_PROM_DIR = "D:\Steam\steamapps\common\Monster Prom"
.\build.ps1
```

Das Skript erzeugt:

- `dist\MonsterPromHelper.Ingame.dll`
- `install-pack\` — fertige Ordnerstruktur zum Kopieren

**Build-Fehler „BepInEx nicht gefunden“?**  
Zuerst Schritt 1 abschließen (BepInEx muss im Spielordner unter `BepInEx\core\BepInEx.dll` liegen), dann `build.ps1` erneut ausführen.

### 3. Plugin ins Spiel kopieren

Alles aus `install-pack\` in den **Monster-Prom-Ordner** kopieren (Ordner mergen), sodass es so aussieht:

```
Monster Prom\
  BepInEx\
    plugins\
      MonsterPromHelper\
        MonsterPromHelper.Ingame.dll
        data\
          events_db.json
          secret_endings.json
```

Die JSON-Dateien kommen aus dem Hauptprojekt (`MonsterPromHelper\data\`). Nach Updates am Helper: `build.ps1` erneut ausführen oder nur `data\` neu kopieren.

### 4. Spielen

1. Monster Prom starten.
2. In `BepInEx\LogOutput.log` sollte stehen: `Monster Prom Helper (Ingame) v1.0.0` / Plugin loaded.
3. Runde starten, bei einem Dialog mit zwei Antworten: **Insert** drücken.

## Steuerung & Config

| Taste (Standard) | Aktion |
|------------------|--------|
| **Insert** | Overlay ein / aus |

Config-Datei (nach erstem Start):

`BepInEx\config\com.monsterprom.helper.ingame.cfg`

```ini
[Overlay]

## Taste zum Ein-/Ausblenden des Overlays
ToggleKey = Insert

## Overlay auch anzeigen wenn kein Event aktiv ist
ShowWhenNoEvent = false
```

## Vergleich: Python-Overlay vs. BepInEx

| | Python (`start_overlay.bat`) | BepInEx (dieser Ordner) |
|--|------------------------------|-------------------------|
| Installation | Python + pip | BepInEx + DLL kopieren |
| Event erkennen | OCR / RAM / Suche | **Direkt aus dem Spiel** |
| Stats | Manuell / Memory-Scan | **Automatisch live** |
| Secret-Toasts | Ja (Log-Watcher) | Nur im Overlay (Secret-Hinweis) |

Du kannst **beides** parallel nutzen: BepInEx im Spiel, Python-Agent für Hintergrund-Toasts aus dem Log.

## Ordnerstruktur

```
bepinex-plugin/
  README.md                 ← diese Anleitung
  build.ps1                 ← baut DLL + install-pack
  MonsterPromHelper.Ingame/ ← Plugin-Quellcode (C#)
  dist/                     ← gebaute DLL (nach build)
  install-pack/             ← zum Kopieren ins Spiel (nach build)
  lib/                      ← optional: BepInEx.dll für Build-Kopie
```

## Fehlerbehebung

| Problem | Lösung |
|---------|--------|
| Nach Start nur `BepInEx/core`, kein `LogOutput.log` | **x86-BepInEx** installieren (Monster Prom ist 32-bit), nicht x64 — `.\install-bepinex.ps1` |
| Spiel startet nicht / kein BepInEx-Log | `winhttp.dll` + `doorstop_config.ini` im gleichen Ordner wie `MonsterProm.exe`; ggf. Windows „Zulassen“ / Antivirus-Ausnahme |
| Plugin lädt nicht | `MonsterPromHelper.Ingame.dll` unter `BepInEx\plugins\MonsterPromHelper\`? |
| „0 Events“ im Log | `data\events_db.json` fehlt — `build.ps1` oder `data\` manuell kopieren |
| Log: AN und sofort AUS | Meist **zwei Plugin-DLLs** — `.\fix-double-plugin.ps1` ausführen, Spiel neu starten |
| Overlay unsichtbar (Log: AN) | Ab v1.1.4: Log braucht `Overlay-Text:` + `Overlay gezeichnet (IMGUI)` — sonst alte DLL oder Fehlerzeile |
| `MissingMethodException` (IReadOnly*, IsNullOrWhiteSpace, Array.Empty) | Unity = **CLR 2.0** — Plugin ≥ **1.1.4** installieren (Spiel beenden, DLL kopieren) |
| Overlay leer / nur Rahmen | Footer muss „Datenbank: 433 Events“ zeigen; sonst `data/events_db.json` kopieren |
| Mausklick tut nichts | Nur **F8** oder **F9** (Insert verursacht oft doppeltes AN/AUS) |
| Secret-Ending Toast | Ab **v1.4.2**: nur bei **aktivem Dialog** + **beiden** Antworten passend zu einem Secret-Event aus `secret_endings.json` (kein Zufalls-Match mehr). Config: `Alerts.SecretEndingToast` |
| Pick-Hint (ohne F8) | Ab **v1.5.0**: gruenes Popup **unten rechts** waehrend Dialogen (`EMPFOHLEN: Option X`). Config: `Alerts.PickHint` |
| Stats / Zuneigung ändern | Ab **v1.4.0**: im Overlay (F8) nach unten scrollen — Stats, **Love** und **Interest** pro LI (Damien, Liam, …), `Set` oder +/- |
| Overlay leer bei Dialog | Warte bis **beide Antwort-Buttons** sichtbar sind; Stats-Zeile muss Werte zeigen |
| Build: Assembly-CSharp fehlt | `MONSTER_PROM_DIR` auf installiertes Monster Prom setzen |

## Entwicklung

- GUID: `com.monsterprom.helper.ingame`
- Liest `EventManager.Instance`, `StatsManager.GetStatInt`, `GameManager.CurrentPlayerColor`
- UI: Unity **IMGUI** (`OnGUI`), kein separates Fenster außerhalb des Spiels

---

## Monster Prom 4 (Monster Con)

Separates Plugin für **Monster Prom 4: Monster Con** — gleiches Overlay-Konzept, angepasst an MP4-Mechanik (**Stat-Austausch**: Option zeigt `+CREATIVE / -SMARTS`).

### Build

```powershell
cd "C:\Users\matti\Downloads\MonsterPromHelper\bepinex-plugin"
.\build-mp4.ps1
```

Anderer Spielordner:

```powershell
$env:MONSTER_CON_DIR = "D:\Steam\steamapps\common\Monster Prom 4 - Monster Con"
.\build-mp4.ps1
```

Standard-Pfad: `C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 4 - Monster Con\`  
Start-EXE: **`MonsterCon.exe`** (32-bit, x86-BepInEx).

### Installation

Inhalt von `install-pack-mp4\` in den MP4-Ordner kopieren:

```
Monster Prom 4 - Monster Con\
  BepInEx\
    plugins\
      MonsterProm4Helper\
        MonsterProm4Helper.Ingame.dll
        data\
          events_db.json
          pregame_db.json
          secret_endings.json
```

### MP4 vs MP1

| | MP1 Helper | MP4 Helper |
|--|-----------|------------|
| DLL | `MonsterPromHelper.Ingame.dll` | `MonsterProm4Helper.Ingame.dll` |
| GUID | `com.monsterprom.helper.ingame` | `com.monsterprom4.helper.ingame` |
| Szene | `InGame_School` | `MainGame` |
| Event-Infos | DB + StatRequired | **Live Stat-Austausch** (+/- pro Option) |
| LIs | Damien, Liam, … | Liam, Zoe, Omen, Doug, Nico, April |
| Stats | inkl. MONEY | SMARTS, BOLD, CREATIVE, CHARM, FUN |
| Interest-Editor | Love + Interest | nur **Love** (Dates-Zähler nur Anzeige) |

### Steuerung

- **F8** / **F9** — Overlay (wie MP1)
- Pick-Hint unten rechts während Dialogen
- Stats-Editor im Overlay (scrollen nach unten)

`events_db.json` (87 Events aus dem [Steam Event Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3470379401)) und `pregame_db.json` (Stat-Picks + LI-Anforderungen) liegen unter `data-mp4/` und werden beim Build kopiert. MP1 nutzt weiterhin `data/events_db.json` (~433 Events). Secret-Toasts nutzen `secret_endings.json` (derzeit leer).

---

## Monster Camp Helper (Monster Prom 2)

Separates Plugin für **Monster Prom 2: Monster Camp** — Event-Empfehlungen wie MP1, Daten aus der [Community-Spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit).

### Build

```powershell
cd "C:\Users\matti\Downloads\MonsterPromHelper\bepinex-plugin"
.\build-camp.ps1
```

Anderer Spielordner:

```powershell
$env:MONSTER_CAMP_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom 2 - Monster Camp"
.\build-camp.ps1
```

Standard-Pfad: `C:\Program Files (x86)\Steam\steamapps\common\Monster Prom 2 - Monster Camp\`  
Start-EXE: **`MonsterCamp.exe`** (32-bit, x86-BepInEx).

### Installation

Inhalt von `install-pack-camp\` in den Camp-Ordner kopieren (nach BepInEx-Installation):

```
Monster Prom 2 - Monster Camp\
  BepInEx\
    plugins\
      MonsterCampHelper\
        MonsterCampHelper.Ingame.dll
        data\
          events_db.json      (~270 Events)
          drinks_db.json      (63 Drinks)
          secret_endings.json
```

### Features

- **F8** / **F9** — Overlay mit Stats, Love, Event-Empfehlung
- **Pick-Hint** — kleines Popup bei Dialogen: empfohlene Option nach deinen Stats (ohne F8)
- **Secret-Toasts** — Hinweis bei Secret-Ending-Events
- **Ctrl + Linksklick auf Drink** — zeigt Effekt aus `drinks_db.json` (Juan-Drinks / Drink-Auswahl)

Daten werden beim Build aus dem Google Sheet geladen (`tools/build_camp_db.py` → `data-camp/`).

Config: `BepInEx/config/com.monstercamp.helper.ingame.cfg` (`PickHint`, `DrinkInfo`, `SecretEndingToast`, …).
