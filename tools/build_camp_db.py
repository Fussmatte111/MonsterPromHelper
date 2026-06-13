"""Build Monster Camp event/drink/secret databases from Google Sheets."""

from __future__ import annotations

import csv
import io
import json
import re
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / "data-camp"
SHEET_ID = "1dvqS63ssINhneJGm9hi3V2tGTm5kF1utv1cl8cdtfJ0"
BASE = f"https://docs.google.com/spreadsheets/d/{SHEET_ID}/gviz/tq?tqx=out:csv&sheet="

STAT_ALIASES = {
    "CHARM": "CHARM",
    "CHRAM": "CHARM",
    "SMARTS": "SMARTS",
    "SMART": "SMARTS",
    "SMARST": "SMARTS",
    "BOLDNESS": "BOLD",
    "BOLD": "BOLD",
    "CREATIVITY": "CREATIVE",
    "CREATIVE": "CREATIVE",
    "FUN": "FUN",
}


def fetch_sheet(name: str) -> str:
    url = BASE + urllib.parse.quote(name)
    with urllib.request.urlopen(url, timeout=60) as resp:
        return resp.read().decode("utf-8", errors="replace")


def normalize_stat(raw: str) -> str | None:
    key = (raw or "").strip().upper()
    if not key:
        return None
    return STAT_ALIASES.get(key)


def slug_key(name: str) -> str:
    s = re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")
    return s or "event"


def parse_events_csv(text: str) -> dict[str, dict]:
    reader = csv.reader(io.StringIO(text))
    rows = list(reader)
    if not rows:
        return {}

    events: dict[str, dict] = {}
    for row in rows[1:]:
        if len(row) < 8:
            continue
        num = (row[0] or "").strip()
        name = (row[1] or "").strip()
        location = (row[2] or "").strip()
        characters = (row[3] or "").strip()
        stat1 = normalize_stat(row[4] if len(row) > 4 else "")
        hint1 = (row[5] if len(row) > 5 else "").strip()
        stat2 = normalize_stat(row[6] if len(row) > 6 else "")
        hint2 = (row[7] if len(row) > 7 else "").strip()
        if not name or not stat1 or not stat2:
            continue

        route = characters.split("(")[0].split(",")[0].strip()
        options = [
            {"option": 1, "stat": stat1, "hint": hint1[:160]},
            {"option": 2, "stat": stat2, "hint": hint2[:160]},
        ]
        key = slug_key(name)
        rec = {
            "name": name,
            "route": route,
            "type": location,
            "characters": characters,
            "number": num,
            "options": options,
            "source": "camp_spreadsheet",
        }
        events[key] = rec
        events[name.lower()] = rec
    return events


def parse_drinks_csv(text: str) -> dict[str, dict]:
    reader = csv.reader(io.StringIO(text))
    rows = list(reader)
    drinks: dict[str, dict] = {}
    for row in rows[1:]:
        if not row:
            continue
        name = (row[0] or "").strip()
        if not name:
            continue
        effect = (row[2] if len(row) > 2 else "").strip()
        misc = (row[3] if len(row) > 3 else "").strip()
        key = slug_key(name)
        rec = {
            "name": name,
            "effect": effect,
            "misc": misc,
            "source": "camp_spreadsheet",
        }
        drinks[key] = rec
        drinks[name.lower()] = rec
    return drinks


def parse_secrets_csv(text: str) -> dict:
    reader = csv.reader(io.StringIO(text))
    rows = list(reader)
    by_route: dict[str, list[str]] = {}
    all_names: list[str] = []
    chains: list[dict] = []

    for row in rows[1:]:
        if len(row) < 2:
            continue
        ending = (row[0] or "").strip()
        characters = (row[1] or "").strip()
        event_name = (row[3] if len(row) > 3 else "").strip()
        if not event_name:
            continue
        route = characters.split("(")[0].split(",")[0].strip() or ending
        by_route.setdefault(route, [])
        if event_name not in by_route[route]:
            by_route[route].append(event_name)
        if event_name not in all_names:
            all_names.append(event_name)

    for route, chain in sorted(by_route.items()):
        if chain:
            chains.append(
                {
                    "wiki_title": route,
                    "character": route,
                    "events": chain,
                }
            )

    return {
        "source": "camp_spreadsheet",
        "all": all_names,
        "by_route": by_route,
        "chains": chains,
    }


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    events = parse_events_csv(fetch_sheet("Events and Outcomes"))
    drinks = parse_drinks_csv(fetch_sheet("Drinks"))
    secrets = parse_secrets_csv(fetch_sheet("Secret Endings"))

    events_path = OUT_DIR / "events_db.json"
    drinks_path = OUT_DIR / "drinks_db.json"
    secrets_path = OUT_DIR / "secret_endings.json"

    events_path.write_text(json.dumps(events, indent=2, ensure_ascii=False), encoding="utf-8")
    drinks_path.write_text(json.dumps(drinks, indent=2, ensure_ascii=False), encoding="utf-8")
    secrets_path.write_text(json.dumps(secrets, indent=2, ensure_ascii=False), encoding="utf-8")

    unique_events = len({v["name"] for v in events.values() if "name" in v})
    unique_drinks = len({v["name"] for v in drinks.values() if "name" in v})
    print(f"Wrote {events_path} ({unique_events} events)")
    print(f"Wrote {drinks_path} ({unique_drinks} drinks)")
    print(f"Wrote {secrets_path} ({len(secrets['all'])} secret events)")


if __name__ == "__main__":
    main()
