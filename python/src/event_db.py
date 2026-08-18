"""Load and query Monster Prom event database."""

from __future__ import annotations

import json
import re
from difflib import SequenceMatcher
from pathlib import Path


# Same order as in-game stat sheet: Smarts, Boldness, Creativity, Charm, Fun, Money
STAT_KEYS = ("SMARTS", "BOLD", "CREATIVE", "CHARM", "FUN", "MONEY")


class EventDatabase:
    def __init__(self, path: Path) -> None:
        self.path = path
        self.events: dict[str, dict] = {}
        if path.exists():
            self.events = json.loads(path.read_text(encoding="utf-8"))

    def get(self, name: str) -> dict | None:
        return self.events.get(name.lower()) or self.events.get(name)

    def search(self, query: str, limit: int = 25) -> list[dict]:
        q = query.strip().lower()
        if not q:
            return []
        results = []
        for key, ev in self.events.items():
            name = ev.get("name", key)
            if q in key or q in name.lower():
                results.append(ev)
                if len(results) >= limit:
                    break
        return results

    def _rank_dialog(self, query: str, limit: int = 10) -> list[tuple[int, dict]]:
        q = query.strip().lower()
        if len(q) < 2:
            return []

        words = [w for w in re.split(r"[^a-z0-9]+", q) if len(w) >= 3]
        if not words:
            words = [q]

        scored: list[tuple[int, dict]] = []
        for key, ev in self.events.items():
            name = ev.get("name", key)
            route = (ev.get("route") or "").lower()
            etype = (ev.get("type") or "").lower()
            hints = " ".join(
                (o.get("hint") or "").lower() for o in ev.get("options", [])
            )
            haystack = f"{name.lower()} {key} {route} {etype} {hints}"

            score = 0
            if q in name.lower() or q in key:
                score += 8
            for word in words:
                if word in haystack:
                    score += 3
                if word in hints:
                    score += 4
                if word in route:
                    score += 2

            if score > 0:
                scored.append((score, ev))

        scored.sort(key=lambda item: (-item[0], len(item[1].get("name", ""))))
        return scored[:limit]

    def search_dialog(self, query: str, limit: int = 10) -> list[dict]:
        """Search by words from visible dialog — hints, route, LI names, event name."""
        return [ev for _, ev in self._rank_dialog(query, limit)]

    def best_dialog_match(
        self, query: str, limit: int = 8
    ) -> tuple[dict | None, list[dict]]:
        """Pick one event when OCR/text match is strong enough to auto-apply."""
        ranked = self._rank_dialog(query, limit)
        if not ranked:
            return None, []

        best_score, best = ranked[0]
        if best_score < 8:
            return None, [ev for _, ev in ranked]

        if len(ranked) == 1:
            return best, [ev for _, ev in ranked]

        second_score = ranked[1][0]
        if best_score >= second_score + 5 and best_score >= 11:
            return best, [ev for _, ev in ranked]
        if best_score >= 16:
            return best, [ev for _, ev in ranked]
        return None, [ev for _, ev in ranked]

    def _hint_tokens(self, hint: str) -> list[str]:
        return [w for w in re.split(r"[^a-z0-9]+", hint.lower()) if len(w) >= 3]

    def _hint_line_score(self, hint: str, line: str) -> int:
        if not hint or not line:
            return 0
        h = hint.lower().strip()
        ln = line.lower().strip()
        score = 0
        if h in ln or ln in h:
            score += 12
        ratio = SequenceMatcher(None, h, ln).ratio()
        score += int(ratio * 10)
        for word in self._hint_tokens(hint):
            if word in ln:
                score += 3
        return score

    def match_screenshot(
        self,
        ocr_text: str,
        option1_line: str = "",
        option2_line: str = "",
    ) -> tuple[dict | None, list[dict], str, str]:
        """
        Match event from screenshot OCR: option lines first, then full-text fallbacks.
        Returns (best, hits, status, detail_message).
        """
        best, hits, status = self.match_by_two_options(
            ocr_text, option1_line, option2_line, relaxed=True
        )
        if best and status == "ok":
            return best, hits, status, ""

        combined = " ".join(
            part
            for part in (option1_line, option2_line, ocr_text)
            if part and part.strip()
        ).strip()
        if combined:
            best, hits = self.best_dialog_match(combined, limit=10)
            if best:
                return best, hits, "ok", "Full-text search"
            if hits:
                return None, hits, "multiple", "Multiple hits — check the list"
            hits = self.search_dialog(combined, limit=10)
            if len(hits) == 1:
                return hits[0], hits, "ok", "Keyword search"
            if hits:
                return None, hits, "multiple", "Keyword — check the list"

        return None, [], "no_hit", "OCR text matches no event in the DB"

    def match_by_two_options(
        self,
        ocr_text: str,
        option1_line: str = "",
        option2_line: str = "",
        limit: int = 12,
        *,
        relaxed: bool = False,
    ) -> tuple[dict | None, list[dict], str]:
        """
        Find event where both answer hints match OCR (best: two option lines).
        Returns (best_event, ranked_hits, status_message).
        """
        hay = (ocr_text or "").lower()
        a = (option1_line or "").strip()
        b = (option2_line or "").strip()
        has_lines = len(a) >= 3 and len(b) >= 3

        scored: list[tuple[int, dict]] = []
        for key, ev in self.events.items():
            opts = ev.get("options") or []
            if len(opts) < 2:
                continue
            h1 = (opts[0].get("hint") or "").strip()
            h2 = (opts[1].get("hint") or "").strip()
            if not h1 or not h2:
                continue

            min_line = 8 if relaxed else 10
            min_full = 4 if relaxed else 5
            if has_lines:
                assign_ab = self._hint_line_score(h1, a) + self._hint_line_score(h2, b)
                assign_ba = self._hint_line_score(h1, b) + self._hint_line_score(h2, a)
                score = max(assign_ab, assign_ba)
                if score < min_line:
                    continue
            else:
                s1 = self._hint_line_score(h1, hay)
                s2 = self._hint_line_score(h2, hay)
                if s1 < min_full or s2 < min_full:
                    continue
                score = s1 + s2

            scored.append((score, ev))

        scored.sort(key=lambda item: (-item[0], item[1].get("name", "")))
        hits = [ev for _, ev in scored[:limit]]
        if not hits:
            msg = "No event matches the recognized choices"
            if has_lines:
                msg += f" — read: „{a[:40]}“ / „{b[:40]}“"
            return None, [], msg

        best_score, best = scored[0]
        second = scored[1][0] if len(scored) > 1 else 0
        pick_score = 11 if relaxed else 14
        gap = 3 if relaxed else 4
        solo_score = 14 if relaxed else 18

        if best_score >= pick_score and (len(scored) == 1 or best_score >= second + gap):
            return best, hits, "ok"

        if best_score >= solo_score:
            return best, hits, "ok"

        return None, hits, "multiple"

    def all_names(self) -> list[str]:
        return sorted(ev.get("name", k) for k, ev in self.events.items())


def recommend_option(event: dict, stats: dict[str, int]) -> list[dict]:
    """Return options annotated with success likelihood vs other option stats."""
    options = event.get("options", [])
    if not options:
        return []

    annotated = []
    for opt in options:
        stat = opt.get("stat", "")
        value = stats.get(stat, 0)
        other_stats = [o["stat"] for o in options if o["stat"] != stat]
        rival_values = [stats.get(s, 0) for s in set(other_stats)]
        max_rival = max(rival_values) if rival_values else 0
        if value > max_rival:
            verdict = "success"
        elif value == max_rival:
            verdict = "tie"
        else:
            verdict = "fail"

        annotated.append(
            {
                **opt,
                "value": value,
                "verdict": verdict,
            }
        )

    best = max(annotated, key=lambda o: o["value"])
    for o in annotated:
        o["recommended"] = o["option"] == best["option"] and o["verdict"] == "success"

    return annotated


def best_option(annotated: list[dict]) -> dict | None:
    winners = [o for o in annotated if o.get("recommended")]
    if winners:
        return winners[0]
    if annotated:
        return max(annotated, key=lambda o: o.get("value", 0))
    return None
