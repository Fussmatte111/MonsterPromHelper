# Monster Prom Helper

Unofficial fan tools for the **Monster Prom** series (Beautiful Glitch): in-game overlays via **BepInEx**, plus an optional **Python log agent** with Windows toasts.

> Not affiliated with Beautiful Glitch. Use at your own risk; mods can affect achievements or online features.

Repository: [github.com/Fussmatte111/MonsterPromHelper](https://github.com/Fussmatte111/MonsterPromHelper)

---

## Two components

| | **BepInEx plugins** *(recommended)* | **Python tools** *(optional)* |
|--|-------------------------------------|-------------------------------|
| **What** | In-game overlay, pick hints, secret alerts | Log watcher + desktop overlay |
| **Data source** | Live game state (stats, dialog UI) | Unity log, optional OCR / RAM |
| **Docs** | Per-game README under `games/` | [`python/README.md`](python/README.md) |
| **Start** | Copy the install pack into the game folder | `start.bat` / `start_overlay.bat` |

Both can run at the same time: BepInEx for precise in-game picks, Python for background toasts.

---

## Supported games

| Game | Folder | Plugin | Python (`config.json`) |
|------|--------|--------|-------------------------|
| **Monster Prom** (1) | [`games/monster-prom/`](games/monster-prom/README.md) | `MonsterPromHelper` | `"game": "Monster Prom"` |
| **Monster Prom 2: Monster Camp** | [`games/monster-camp/`](games/monster-camp/README.md) | `MonsterCampHelper` | `"game": "Monster Camp"` |
| **Monster Prom 4: Monster Con** | [`games/monster-con/`](games/monster-con/README.md) | `MonsterProm4Helper` | *(BepInEx first)* |

Typical Steam folders (`steamapps/common/`):

| Game | Folder |
|------|--------|
| Monster Prom | `Monster Prom/` |
| Monster Camp | `Monster Prom 2 - Monster Camp/` |
| Monster Con | `Monster Prom 4 - Monster Con/` |

Each game needs **its own BepInEx** in that install folder. Plugins are not interchangeable.

---

## Quick start — BepInEx *(recommended)*

1. Unpack [BepInEx 5.4 x86](https://github.com/BepInEx/BepInEx/releases) into the game folder.
2. Launch the game once so `BepInEx/plugins/` is created.
3. Copy the matching `games/<game>/install-pack/` into the same folder (merge).
4. Press **F8** in-game for the overlay and pick recommendation.

Full install, build scripts, and troubleshooting live in each game README.

---

## Quick start — Python

```powershell
.\start.ps1
```

Or double-click `start.bat`. On first run this copies `python/config.example.json` to `python/config.json`.

Desktop overlay (OCR / search):

```powershell
.\start_overlay.bat
```

Details: [`python/README.md`](python/README.md)

---

## Layout

```
MonsterPromHelper/
  README.md
  start.bat / start.ps1 / start_overlay.bat   ← wrappers into python/
  games/
    monster-prom/     plugin, data, install-pack, build.ps1
    monster-camp/
    monster-con/
  python/             optional log watcher + desktop overlay
  tools/              scripts that rebuild event databases
```

---

## Event data & secret endings

| Game | Data | Source |
|------|------|--------|
| Monster Prom | `games/monster-prom/data/` | [Steam Event Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=2043551842) |
| Monster Camp | `games/monster-camp/data/` | [Community spreadsheet](https://docs.google.com/spreadsheets/d/1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0/edit) |
| Monster Con | `games/monster-con/data/` | [Steam Guide](https://steamcommunity.com/sharedfiles/filedetails/?id=3470379401), [Wiki pregame](https://monsterprom.wiki.gg/wiki/Pre-Game_Preparation/Monster_Con) |

Rebuild databases:

```powershell
python tools/build_event_db.py         # MP1
python tools/build_camp_db.py          # Camp (needs internet)
python tools/build_mp4_event_db.py     # MP4
python tools/build_mp4_pregame_wiki.py
```

---

## Contributing

Issues and pull requests: [github.com/Fussmatte111/MonsterPromHelper/issues](https://github.com/Fussmatte111/MonsterPromHelper/issues)

## Credits

- Beautiful Glitch — *Monster Prom*
- Community guides, [Monster Prom Wiki](https://monsterprom.wiki.gg/), spreadsheet authors
- [BepInEx](https://github.com/BepInEx/BepInEx)
