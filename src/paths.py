"""Auto-detect Monster Prom log paths on Windows."""

from __future__ import annotations

import os
from pathlib import Path


GAMES = {
    "Monster Prom": "Monster Prom",
    "Monster Camp": "Monster Camp",
    "Monster Prom 2": "Monster Prom 2",
}


def local_low_base() -> Path:
    return Path(os.environ.get("LOCALAPPDATA", "")) / ".." / "LocalLow" / "Beautiful Glitch"


def default_output_log(game: str) -> Path | None:
    folder = GAMES.get(game, game)
    path = (local_low_base() / folder / "output_log.txt").resolve()
    return path if path.exists() else None


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
    folder_names = {
        "Monster Prom": "Monster Prom",
        "Monster Camp": "Monster Camp",
        "Monster Prom 2": "Monster Prom 2",
    }
    install = folder_names.get(game, game)
    for root in steam_roots:
        mplogs = root / install / "MPLogs"
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
            f"output_log.txt für '{game}' nicht gefunden. "
            "Starte das Spiel einmal oder setze output_log_path in config.json."
        )

    mplogs_path = Path(mplogs) if mplogs else default_mplogs(game)
    return log_path, mplogs_path
