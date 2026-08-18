#!/usr/bin/env python3
"""Monster Prom Helper — background log watcher with notifications."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .paths import resolve_paths
from .watcher import GameLogWatcher


def load_config(project_root: Path) -> dict:
    config_path = project_root / "config.json"
    example_path = project_root / "config.example.json"

    if config_path.exists():
        return json.loads(config_path.read_text(encoding="utf-8"))

    if example_path.exists():
        data = json.loads(example_path.read_text(encoding="utf-8"))
        config_path.write_text(json.dumps(data, indent=2), encoding="utf-8")
        print(f"Created config.json from template: {config_path}")
        return data

    return {}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Monster Prom Helper — tracks events and secret endings in the background."
    )
    parser.add_argument(
        "--game",
        default=None,
        help="Game (Monster Prom, Monster Camp, Monster Prom 2)",
    )
    parser.add_argument(
        "--scan-mplogs",
        action="store_true",
        help="Scan MPLogs once (after a run)",
    )
    parser.add_argument(
        "--overlay",
        action="store_true",
        help="Start the dialog overlay window (stats + options)",
    )
    args = parser.parse_args(argv)

    project_root = Path(__file__).resolve().parent.parent
    config = load_config(project_root)
    if args.game:
        config["game"] = args.game

    try:
        log_path, mplogs_path = resolve_paths(config)
    except FileNotFoundError as exc:
        print(exc, file=sys.stderr)
        return 1

    watcher = GameLogWatcher(config, project_root)

    if args.overlay:
        from .overlay_app import main as overlay_main

        return overlay_main()

    if args.scan_mplogs:
        print(f"Scanning MPLogs: {mplogs_path}")
        watcher.scan_mplogs(mplogs_path)
        return 0

    try:
        watcher.tail_file(log_path)
    except KeyboardInterrupt:
        print("\nStopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
