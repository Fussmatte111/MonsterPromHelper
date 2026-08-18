"""Resolve Unity log paths and per-game data folders."""

from __future__ import annotations

import os
from pathlib import Path


GAME_SLUGS = {
    "Monster Prom": "monster-prom",
    "Monster Camp": "monster-camp",
    "Monster Prom 2": "monster-camp",
    "Monster Prom 4": "monster-con",
    "Monster Con": "monster-con",
}

LOCALLOW_FOLDERS = {
    "Monster Prom": ("Monster Prom",),
    "Monster Camp": ("Monster Camp", "Monster Prom 2"),
    "Monster Prom 2": ("Monster Camp", "Monster Prom 2"),
    "Monster Prom 4": ("Monster Prom 4", "Monster Con"),
    "Monster Con": ("Monster Prom 4", "Monster Con"),
}

STEAM_FOLDERS = {
    "Monster Prom": ("Monster Prom",),
    "Monster Camp": ("Monster Prom 2 - Monster Camp", "Monster Camp"),
    "Monster Prom 2": ("Monster Prom 2 - Monster Camp", "Monster Camp"),
    "Monster Prom 4": ("Monster Prom 4 - Monster Con", "Monster Con"),
    "Monster Con": ("Monster Prom 4 - Monster Con", "Monster Con"),
}


def python_root() -> Path:
    return Path(__file__).resolve().parent.parent


def repo_root() -> Path:
    return python_root().parent


def game_data_dir(game: str) -> Path:
    slug = GAME_SLUGS.get(game, "monster-prom")
    return repo_root() / "games" / slug / "data"


def local_low_base() -> Path:
    return Path(os.environ.get("LOCALAPPDATA", "")) / ".." / "LocalLow" / "Beautiful Glitch"


def default_output_log(game: str) -> Path | None:
    folders = LOCALLOW_FOLDERS.get(game, (game,))
    base = local_low_base()
    for folder in folders:
        for name in ("output_log.txt", "Player.log"):
            path = (base / folder / name).resolve()
            if path.exists():
                return path
    return None


def default_mplogs(game: str) -> Path | None:
    steam_roots = [
        Path(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"))
        / "Steam"
        / "steamapps"
        / "common",
        Path(os.environ.get("ProgramFiles", r"C:\Program Files"))
        / "Steam"
        / "steamapps"
        / "common",
    ]
    folders = STEAM_FOLDERS.get(game, (game,))
    for root in steam_roots:
        for folder in folders:
            mplogs = root / folder / "MPLogs"
            if mplogs.is_dir():
                return mplogs
    return None


def resolve_paths(config: dict) -> tuple[Path, Path | None]:
    game = config.get("game", "Monster Prom")
    output_log = config.get("output_log_path")
    mplogs = config.get("mplogs_path")

    log_path = Path(output_log) if output_log else default_output_log(game)
    if log_path is None:
        raise FileNotFoundError(
            f"output_log.txt for '{game}' not found. "
            "Start the game once or set output_log_path in config.json."
        )

    mplogs_path = Path(mplogs) if mplogs else default_mplogs(game)
    return log_path, mplogs_path
