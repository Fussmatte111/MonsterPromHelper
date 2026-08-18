# Monster Prom (1)

BepInEx overlay for **Monster Prom**. Reads dialog, stats, and events **from the game** — no OCR, no RAM guessing.

**F8** / **F9** toggles the overlay.

## Features

- Overlay with event name, choice texts, and a stat pick
- Pick-hint popup during dialogs (no overlay required)
- Secret-ending toasts
- Event match from choice text + internal event names (`events_db.json`)

## Install (no build)

1. Unpack [BepInEx 5.4 x86](https://github.com/BepInEx/BepInEx/releases) into `steamapps/common/Monster Prom/`.
2. Launch the game once, then quit.
3. Copy the contents of `install-pack/` into that same folder (merge).
4. Launch the game. `BepInEx/LogOutput.log` should contain `Helper v… — Plugin loaded`.

After the first launch, config is at `BepInEx/config/com.monsterprom.helper.ingame.cfg`.

## Install automatically

PowerShell **as Administrator** if the game lives under `Program Files`:

```powershell
cd path\to\MonsterPromHelper\games\monster-prom
.\install-bepinex.ps1
```

Downloads BepInEx, detects 32/64-bit, and deploys the plugin.

## Build from source

Needs the **.NET SDK** and BepInEx in the game folder (or the script will fetch `BepInEx.dll` into `lib/`).

```powershell
cd path\to\MonsterPromHelper\games\monster-prom
.\build.ps1
```

Other game folder:

```powershell
$env:MONSTER_PROM_DIR = "D:\SteamLibrary\steamapps\common\Monster Prom"
.\build.ps1
```

Then copy `install-pack\` into the game folder.

## Game notes

- **Scene:** `InGame_School`
- **Rules:** two stats per event; the game picks the higher stat
- **Data:** ~430 events in `data/events_db.json`, secret routes in `secret_endings.json`
- **LIs:** Damien, Liam, Miranda, Polly, Scott, Vera, Calculester, Zoe
- **Stats:** SMARTS, BOLD, CREATIVE, CHARM, FUN, MONEY

Installed files:

```
Monster Prom/
  BepInEx/plugins/MonsterPromHelper/
    MonsterPromHelper.Ingame.dll
    data/events_db.json
    data/secret_endings.json
```

If the overlay flickers on/off, two plugin copies are loaded — run `fix-double-plugin.ps1`. Nested plugin paths: `fix-plugin-path.ps1`.
