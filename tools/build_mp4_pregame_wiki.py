"""Fetch Monster Con pregame data from the Monster Prom Wiki."""

from __future__ import annotations

import json
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "games" / "monster-con" / "data" / "pregame_db.json"
WIKI_URL = (
    "https://monsterprom.wiki.gg/api.php"
    "?action=query&titles=Pre-Game_Preparation/Monster_Con"
    "&prop=revisions&rvprop=content&format=json"
)

STAT_MAP = {
    "Smarts": "SMARTS",
    "Boldness": "BOLD",
    "Creativity": "CREATIVE",
    "Charm": "CHARM",
    "Fun": "FUN",
}

RO_MAP = {
    "April First": "April",
    "Doug Campbell": "Doug",
    "Liam de Lioncourt": "Liam",
    "Nico Sharp": "Nico",
    "Omen": "Omen",
    "Zoe": "Zoe",
}


def normalize_stat(template: str) -> str:
    for key, val in STAT_MAP.items():
        if key in template:
            return val
    return ""


def parse_ros(cell: str) -> list[str]:
    names = re.findall(r"\{\{([^}|]+)", cell)
    out: list[str] = []
    for name in names:
        name = name.strip()
        mapped = RO_MAP.get(name, name.split()[0] if name else "")
        if mapped and mapped not in out:
            out.append(mapped)
    return out


def parse_likes(wikitext: str) -> dict[str, dict]:
    likes: dict[str, dict] = {}
    section = wikitext.split("==Romance Option Likes==", 1)
    if len(section) < 2:
        return likes
    body = section[1].split("==Items==", 1)[0]
    rows = re.findall(
        r"^\|([^\n|][^\n]*)\n\|\{\{([^}]+)\}\}\n(?:![^\n|]*\|)?(.+)$",
        body,
        re.M,
    )
    for option, stat_tpl, ro_cell in rows:
        option = option.strip().strip('"')
        stat = normalize_stat(stat_tpl)
        if not option or not stat:
            continue
        key = re.sub(r"[^a-z0-9]+", "_", option.lower()).strip("_")
        likes[key] = {
            "label": option,
            "stat": stat,
            "characters": parse_ros(ro_cell),
            "hint": option,
        }
    return likes


def parse_item_tables(wikitext: str) -> dict[str, dict]:
    items: dict[str, dict] = {}
    section = wikitext.split("==Items==", 1)
    if len(section) < 2:
        return items

    chunks = re.split(r"\*Choose your top 3 favorite (.+?)\n", section[1])
    for i in range(1, len(chunks), 2):
        category = chunks[i].strip()
        table = chunks[i + 1] if i + 1 < len(chunks) else ""
        cat_key = re.sub(r"[^a-z0-9]+", "_", category.lower()).strip("_")
        options: list[dict] = []
        stat_rows = re.findall(r"\[\[File:([^|\]]+)\|[^\]]*\]\]\n\|\{\{([^}]+)\}\}", table)
        for stat_name, stat_tpl in stat_rows:
            stat = normalize_stat(stat_tpl)
            if stat:
                options.append({"file": stat_name.strip(), "stat": stat})
        if options:
            items[cat_key] = {"category": category, "options": options}
    return items


def fetch_wikitext() -> str:
    req = urllib.request.Request(
        WIKI_URL,
        headers={"User-Agent": "MonsterPromHelper/1.3 (wiki data builder; contact: local dev)"},
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        payload = json.loads(resp.read().decode("utf-8"))
    pages = payload["query"]["pages"]
    page = next(iter(pages.values()))
    return page["revisions"][0]["*"]


def main() -> None:
    wikitext = fetch_wikitext()

    data = {
        "source": "https://monsterprom.wiki.gg/wiki/Pre-Game_Preparation/Monster_Con",
        "likes": parse_likes(wikitext),
        "item_categories": parse_item_tables(wikitext),
        "character_requirements": {
            "Liam": {"prefers": ["CREATIVE", "SMARTS"], "hint": "lots of Creativity and Smarts"},
            "Zoe": {"prefers": ["CREATIVE", "FUN"], "hint": "lots of Creativity and Fun"},
            "Omen": {"prefers": ["BOLD"], "hint": "Boldness; dislikes nerd stats"},
            "Doug": {"prefers": [], "hint": "one stat much higher than others"},
            "Nico": {"prefers": [], "hint": "balanced stats"},
            "April": {"prefers": ["CHARM", "FUN", "CREATIVE"], "hint": "Charm first, then Fun/Creative"},
        },
    }

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {len(data['likes'])} likes, {len(data['item_categories'])} item categories -> {OUT}")


if __name__ == "__main__":
    main()
