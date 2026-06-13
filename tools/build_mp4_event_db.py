"""Build Monster Prom 4 event databases from the Steam Event Guide export."""

from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
GUIDE = Path(
    r"C:\Users\matti\.cursor\projects\c-Users-matti-Downloads-MonsterPromHelper\agent-tools\20607f07-0b3f-4c7a-b394-a6acec9e329a.txt"
)
OUT_EVENTS = ROOT / "data-mp4" / "events_db.json"

STAT_MAP = {
    "SMARTS": "SMARTS",
    "SMART": "SMARTS",
    "BOLDNESS": "BOLD",
    "BOLD": "BOLD",
    "CREATIVITY": "CREATIVE",
    "CREATIVE": "CREATIVE",
    "CHARM": "CHARM",
    "FUN": "FUN",
}

LOCATION_HEADERS = {
    "HOTEL AND OUTDOORS",
    "MAIN STAGE",
    "COMMUNITY ZONE",
    "MERCH HALL",
    "FIRST AID BOOTH",
    "ANY LOCATION (+ UNSURE OF LOCATION)",
    "ANY LOCATION",
}

SKIP_LINES = {
    "",
    "Answers",
    "Stat",
    "Intro",
    "Game Prep",
    "Character Requirements",
    "How to use the guide",
    "TBA",
    "First Option",
    "Second Option",
    "Stat tied to the first option",
    "Stat tied to the second option",
}


def normalize_stat(raw: str) -> str | None:
    key = raw.strip().upper()
    key = key.replace("NESS", "") if key == "BOLD" else key
    if key == "BOLDNESS":
        key = "BOLD"
    if key == "CREATIVITY":
        key = "CREATIVE"
    return STAT_MAP.get(key)


def slug(text: str, max_len: int = 42) -> str:
    s = re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")
    return (s[:max_len] or "event").strip("_")


def non_empty_after(lines: list[str], start: int, limit: int = 8) -> list[str]:
    out: list[str] = []
    i = start + 1
    while i < len(lines) and len(out) < limit:
        text = lines[i].strip()
        if text and text not in SKIP_LINES:
            out.append(text)
        i += 1
    return out


def parse_pregame_blob(text: str) -> dict[str, dict]:
    picks: dict[str, dict] = {}
    for match in re.finditer(
        r"([A-Za-z][A-Za-z0-9' ]{0,28}?)\s*=\s*(Smarts|Boldness|Creativity|Charm|Fun)(?=[A-Z]|$|\s|[,.])",
        text,
        re.I,
    ):
        label = match.group(1).strip()
        stat = normalize_stat(match.group(2))
        if not stat or len(label) < 2:
            continue
        key = slug(label, 32)
        picks[key] = {"label": label, "stat": stat, "hint": match.group(0)[:120]}
    return picks


def parse_character_requirements_blob(text: str) -> dict[str, dict]:
    chars: dict[str, dict] = {}
    for match in re.finditer(
        r"(Liam|Zoe|Omen|Doug|Nico|April)(Requires[^.]+)\.",
        text,
        re.I,
    ):
        char = match.group(1).title()
        if char == "Doug":
            char = "Doug"
        req = match.group(2).replace("Requires", "").strip()
        stats: list[str] = []
        for token in ("Creativity", "Smarts", "Boldness", "Charm", "Fun"):
            if token.lower() in req.lower():
                norm = normalize_stat(token)
                if norm and norm not in stats:
                    stats.append(norm)
        chars[char] = {"prefers": stats, "hint": req[:160]}
    return chars
    if "=" not in line:
        return None
    left, right = line.split("=", 1)
    stat = normalize_stat(right)
    if not stat:
        return None
    return left.strip(), stat


def parse_pregame(lines: list[str]) -> dict:
    pregame: dict = {
        "stat_picks": {},
        "character_requirements": {},
        "notes": [],
    }

    section = ""
    for raw in lines:
        line = raw.strip()
        if not line:
            continue

        low = line.lower()
        if line == "Game Prep":
            section = "prep"
            continue
        if line == "Character Requirements":
            section = "chars"
            continue
        if line.startswith("How to use"):
            section = ""
            continue
        if line in LOCATION_HEADERS or " Secret Endings" in line or line.endswith(" ENDING"):
            section = ""
            continue

        if section == "prep":
            pregame["stat_picks"].update(parse_pregame_blob(line))
            if len(line) > 20 and "tropes" in low:
                pregame["notes"].append(line[:200])

        elif section == "chars":
            pregame["character_requirements"].update(parse_character_requirements_blob(line))
    return pregame


def parse_events(lines: list[str]) -> dict[str, dict]:
    events: dict[str, dict] = {}
    location = "General"
    counters: dict[str, int] = {}
    i = 0
    in_secret = False
    secret_route = ""

    while i < len(lines):
        line = lines[i].strip()

        if line in LOCATION_HEADERS:
            location = line.title()
            in_secret = False
            i += 1
            continue

        if " Secret Endings" in line or line.endswith(" ENDING"):
            in_secret = True
            secret_route = line.replace(" Secret Endings", "").replace(" ENDING", "").strip()
            i += 1
            continue

        if line.startswith("Event ") and " - " in line:
            i += 1
            continue

        if line != "Answers":
            i += 1
            continue

        chunk = non_empty_after(lines, i, 5)
        if len(chunk) < 4:
            i += 1
            continue
        if chunk[0] in {"First Option", "Second Option"}:
            i += 1
            continue

        opt1 = chunk[0]
        stat1 = normalize_stat(chunk[1])
        opt2 = chunk[2]
        stat2 = normalize_stat(chunk[3])

        if not opt1 or opt1 in SKIP_LINES or not stat1:
            i += 1
            continue
        if not opt2 or opt2 in SKIP_LINES or opt2.upper() == "TBA" or not stat2:
            i += 1
            continue

        loc_key = slug(location, 24)
        counters[loc_key] = counters.get(loc_key, 0) + 1
        key = f"{loc_key}_{counters[loc_key]:03d}"
        name = key

        event_type = "Stat Exchange"
        route = location
        if in_secret:
            event_type = "Secret Stat Exchange"
            route = secret_route or location

        events[key] = {
            "name": name,
            "route": route,
            "type": event_type,
            "location": location,
            "phase": "pregame" if "prologue" in location.lower() else "con",
            "options": [
                {
                    "option": 1,
                    "stat": stat1,
                    "lose": stat2,
                    "hint": opt1[:160],
                },
                {
                    "option": 2,
                    "stat": stat2,
                    "lose": stat1,
                    "hint": opt2[:160],
                },
            ],
            "source": "steam_guide_mp4",
        }

        i += 1

    return events


def main() -> None:
    if not GUIDE.exists():
        raise SystemExit(f"Guide export missing: {GUIDE}")

    lines = GUIDE.read_text(encoding="utf-8", errors="replace").splitlines()
    events = parse_events(lines)

    OUT_EVENTS.parent.mkdir(parents=True, exist_ok=True)
    OUT_EVENTS.write_text(json.dumps(events, indent=2, ensure_ascii=False), encoding="utf-8")

    print(f"Wrote {len(events)} con events -> {OUT_EVENTS}")


if __name__ == "__main__":
    main()
