# Monster Prom 2: Monster Camp

BepInEx overlay for **Monster Camp**. Reads dialog, stats, drinks, and events **from the game**.

**F8** / **F9** toggles the overlay. **Ctrl + left-click** a drink to show its effect.

## Features

- Overlay with event name, choice texts, and a stat pick
- Pick-hint popup during dialogs
- Secret-ending toasts
- Drink info from `drinks_db.json`

## Install (no build)

1. Unpack [BepInEx 5.4 x86](https://github.com/BepInEx/BepInEx/releases) into `steamapps/common/Monster Prom 2 - Monster Camp/`.
2. Launch the game once, then quit.
3. Copy the contents of `install-pack/` into that same folder (merge).
4. Launch the game. `BepInEx/LogOutput.log` should mention the helper.

Config: `BepInEx/config/com.monstercamp.helper.ingame.cfg`.

## Build from source

Needs the **.NET SDK**. The script also rebuilds event/drink data from the [community spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit) (internet required).

```powershell
cd path\to\MonsterPromHelper\games\monster-camp
.\build.ps1
```

Other game folder:

```powershell
$env:MONSTER_CAMP_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom 2 - Monster Camp"
.\build.ps1
```

## Game notes

- **Scene:** `MainGame`
- **Rules:** same as MP1 (higher stat wins)
- **Data:** ~270 events, 63 drinks, secret events
- **LIs:** Damien, Calculester, Milo, Dahlia, Joy, Aaravi
- **Stats:** SMARTS, BOLD, CREATIVE, CHARM, FUN (no MONEY)

Installed files:

```
Monster Prom 2 - Monster Camp/
  BepInEx/plugins/MonsterCampHelper/
    MonsterCampHelper.Ingame.dll
    data/events_db.json
    data/drinks_db.json
    data/secret_endings.json
```
