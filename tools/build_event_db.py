"""Build events database from Steam guide export + workshop TSV mods."""

from __future__ import annotations

import json
import re
from pathlib import Path

STAT_NAMES = {
    "CHARM",
    "SMARTS",
    "CREATIVE",
    "BOLD",
    "BOLDNESS",
    "FUN",
    "MONEY",
}

ROOT = Path(__file__).resolve().parent.parent
GUIDE = Path(
    r"C:\Users\matti\.cursor\projects\c-Users-matti-Downloads-MonsterPromHelper\agent-tools\d9e33e32-e4b5-48f2-b8b2-13399df11a1d.txt"
)
OUT = ROOT / "games" / "monster-prom" / "data" / "events_db.json"
MODS_DIR = Path.home() / "AppData/LocalLow/Beautiful Glitch/Monster Prom/Mods"


def normalize_stat(name: str) -> str:
    n = name.strip().upper()
    if n == "BOLDNESS":
        return "BOLD"
    return n


def parse_guide(path: Path) -> dict[str, dict]:
    if not path.exists():
        return {}
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    events: dict[str, dict] = {}
    i = 0
    while i < len(lines):
        if lines[i].strip() != "Event name":
            i += 1
            continue
        if i + 2 >= len(lines):
            break
        name = lines[i + 2].strip()
        if not name or name == "-":
            i += 1
            continue

        options: list[dict] = []
        route = None
        event_type = None
        j = i + 3
        while j < len(lines) and j < i + 80:
            key = lines[j].strip()
            if key == "Event name":
                break
            if key == "Route" and j + 2 < len(lines):
                route = lines[j + 2].strip()
            if key == "Event type" and j + 2 < len(lines):
                event_type = lines[j + 2].strip()
            if key.startswith("Option ") and j + 2 < len(lines):
                opt_num = key.split()[-1]
                stat = normalize_stat(lines[j + 2].strip())
                if stat in STAT_NAMES or stat == "MONEY":
                    hint = ""
                    if j + 4 < len(lines):
                        hint = lines[j + 4].strip()[:120]
                    options.append(
                        {
                            "option": int(opt_num),
                            "stat": stat,
                            "hint": hint,
                        }
                    )
            j += 1

        if options:
            key = name.lower()
            events[key] = {
                "name": name,
                "route": route,
                "type": event_type,
                "options": options[:4],
                "source": "steam_guide",
            }
        i = j
    return events


def parse_mod_tsv(path: Path) -> dict[str, dict]:
    text = path.read_text(encoding="utf-8", errors="replace")
    if "CHOICE_OPTION" not in text:
        return {}
    # First line often event id/name
    first = text.splitlines()[0].lstrip(">").strip()
    name = first.split()[0] if first else path.parent.name
    options = []
    for line in text.splitlines():
        m = re.match(r"CHOICE_OPTION(\d+)\s+(\w+)\s+(.*)", line.strip())
        if m:
            options.append(
                {
                    "option": int(m.group(1)),
                    "stat": normalize_stat(m.group(2)),
                    "hint": m.group(3).strip()[:120],
                }
            )
    if not options:
        return {}
    return {
        name.lower(): {
            "name": name,
            "route": None,
            "type": "workshop_mod",
            "options": options,
            "source": str(path),
        }
    }


def main() -> None:
    events = parse_guide(GUIDE)
    if MODS_DIR.is_dir():
        for tsv in MODS_DIR.rglob("events.tsv"):
            events.update(parse_mod_tsv(tsv))

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(events, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {len(events)} events to {OUT}")


if __name__ == "__main__":
    main()
