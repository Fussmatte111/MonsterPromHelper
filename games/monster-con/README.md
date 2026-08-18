# Monster Prom 4: Monster Con

BepInEx overlay for **Monster Con**. Reads dialog, stats, and events **from the game**.

**F8** / **F9** toggles the overlay.

## Features

- Overlay with event name, choice texts, and a **stat-swap** pick (+STAT / −STAT)
- Pick-hint popup during dialogs
- Secret-ending toasts
- Pregame hints (tropes / likes)
- Badges on dialog choice boxes

## Install (no build)

1. Unpack [BepInEx 5.4 x86](https://github.com/BepInEx/BepInEx/releases) into `steamapps/common/Monster Prom 4 - Monster Con/`.
2. Launch the game once, then quit.
3. Copy the contents of `install-pack/` into that same folder (merge).
4. Launch the game. `BepInEx/LogOutput.log` should mention the helper.

Config: `BepInEx/config/com.monsterprom4.helper.ingame.cfg`.

## Build from source

Needs the **.NET SDK**. If the game folder is found, the script also copies the plugin there.

```powershell
cd path\to\MonsterPromHelper\games\monster-con
.\build.ps1
```

Other game folder:

```powershell
$env:MONSTER_CON_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom 4 - Monster Con"
.\build.ps1
```

The build can refresh `events_db.json` and `pregame_db.json` via `tools/` (Steam guide export + wiki).

## Game notes

- **Scene:** `MainGame` (prologue supported)
- **Rules:** **stat swap** per option, not a simple highest-stat pick
- **Data:** events from the [Steam Event Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3470379401), pregame from the [wiki](https://monsterprom.wiki.gg/wiki/Pre-Game_Preparation/Monster_Con)
- **LIs:** Liam, Zoe, Omen, Doug, Nico, April

Installed files:

```
Monster Prom 4 - Monster Con/
  BepInEx/plugins/MonsterProm4Helper/
    MonsterProm4Helper.Ingame.dll
    data/events_db.json
    data/pregame_db.json
    data/secret_endings.json
```
