"""Parse Monster Prom Unity output_log.txt lines."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

EventKind = Literal[
    "event_outcome",
    "achievement",
    "game_end",
    "interest_lock",
    "plotline",
    "shop_purchase",
    "scene_load",
    "unknown",
]


@dataclass(frozen=True)
class ParsedLine:
    kind: EventKind
    raw: str
    event_id: str | None = None
    event_name: str | None = None
    outcome: str | None = None
    achievement: str | None = None
    player_color: str | None = None
    love_interest: str | None = None
    item: str | None = None
    scene: str | None = None


OUTPUT_CHOSEN = re.compile(
    r"- Output chosen: (?P<outcome>\w+) for (?P<event_id>\d+): (?P<event_name>.+)$"
)
ACHIEVEMENT = re.compile(
    r"\[ STEAM WRAPPER \] : OnAchievementStored\(\) - Achievement '(?P<name>[^']+)' unlocked!"
)
INTEREST_LOCK = re.compile(r"INTEREST LOCK: (?P<color>\w+) -> (?P<li>\w+)")
SHOP = re.compile(r"--- (?P<color>\w+) purchased: <b>(?P<item>[^<]+)</b>")
SCENE_LOAD = re.compile(
    r"LoadScene(?:WithLoading|Directly)\s+(?P<scene>[\w_]+)"
)

# Active gameplay happens in this scene; menus/mod tool do not count.
IN_ROUND_SCENES = frozenset({"InGame_School"})
END_ROUND_SCENES = frozenset(
    {"Credits", "Gallery", "ModTool_MainMenu", "ModTool_Loader", "ModTool_Creator"}
)


def parse_line(line: str) -> ParsedLine | None:
    stripped = line.strip()
    if not stripped or stripped.startswith("(Filename:"):
        return None

    match = OUTPUT_CHOSEN.search(stripped)
    if match:
        return ParsedLine(
            kind="event_outcome",
            raw=stripped,
            event_id=match.group("event_id"),
            event_name=match.group("event_name"),
            outcome=match.group("outcome"),
        )

    match = ACHIEVEMENT.search(stripped)
    if match:
        return ParsedLine(
            kind="achievement",
            raw=stripped,
            achievement=match.group("name"),
        )

    if stripped == "==== GAME END! ====":
        return ParsedLine(kind="game_end", raw=stripped)

    match = INTEREST_LOCK.search(stripped)
    if match:
        return ParsedLine(
            kind="interest_lock",
            raw=stripped,
            player_color=match.group("color"),
            love_interest=match.group("li"),
        )

    if "Big plotline event" in stripped:
        return ParsedLine(kind="plotline", raw=stripped)

    match = SHOP.search(stripped)
    if match:
        return ParsedLine(
            kind="shop_purchase",
            raw=stripped,
            player_color=match.group("color"),
            item=match.group("item"),
        )

    match = SCENE_LOAD.search(stripped)
    if match:
        return ParsedLine(
            kind="scene_load",
            raw=stripped,
            scene=match.group("scene"),
        )

    return None


def bootstrap_from_log(log_path: Path, tail_chars: int = 400_000) -> dict:
    """Infer current scene / round from existing log (overlay starts mid-game)."""
    state: dict = {
        "in_round": False,
        "scene": "",
        "finished_events": set(),
        "player_color": None,
    }
    if not log_path.exists():
        return state

    try:
        size = log_path.stat().st_size
        with log_path.open("r", encoding="utf-8", errors="replace") as f:
            if size > tail_chars:
                f.seek(size - tail_chars)
            text = f.read()
    except OSError:
        return state

    last_scene: str | None = None
    finished_list: list[str] = []
    for line in text.splitlines():
        parsed = parse_line(line)
        if not parsed:
            continue
        if parsed.kind == "scene_load" and parsed.scene:
            last_scene = parsed.scene
        elif parsed.kind == "event_outcome" and parsed.event_name:
            finished_list.append(parsed.event_name.lower())
        elif parsed.kind == "interest_lock" and parsed.player_color:
            state["player_color"] = parsed.player_color
        elif parsed.kind == "game_end":
            last_scene = "Credits"

    if last_scene:
        state["scene"] = last_scene
        state["in_round"] = last_scene in IN_ROUND_SCENES

    state["finished_events"] = set(finished_list[-24:])

    return state
