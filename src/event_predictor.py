"""Predict active dialog event from game memory (before log writes it)."""

from __future__ import annotations

from .event_db import EventDatabase

# Fungus / narrative variables seen in Monster Prom's Assembly-CSharp.dll
FUNGUS_MARKERS = (
    b"currEvent",
    "currEvent".encode("utf-16-le"),
    b"CurrentEvent",
)


class EventPredictor:
    def __init__(self, db: EventDatabase) -> None:
        self._event_names: set[str] = set()
        self._name_by_lower: dict[str, str] = {}
        self._hint_index: list[tuple[str, str]] = []
        self._build_indexes(db)

    def _build_indexes(self, db: EventDatabase) -> None:
        for key, ev in db.events.items():
            name = ev.get("name", key)
            self._event_names.add(name)
            self._name_by_lower[name.lower()] = name
            for opt in ev.get("options", []):
                hint = (opt.get("hint") or "").strip()
                if len(hint) >= 8:
                    self._hint_index.append((name, hint))

    @staticmethod
    def _patterns(text: str) -> list[bytes]:
        return [text.encode("utf-8"), text.encode("utf-16-le")]

    def _name_near_offset(self, chunk: bytes, offset: int, window: int = 280) -> str | None:
        region = chunk[offset : offset + window]
        best: str | None = None
        best_pos = 9999
        for name in self._event_names:
            if len(name) < 4:
                continue
            for pat in self._patterns(name):
                pos = region.find(pat)
                if pos != -1 and pos < best_pos:
                    best_pos = pos
                    best = name
        return best

    def from_fungus_state(self, chunk: bytes) -> str | None:
        """Read currEvent from Fungus variable blob in RAM."""
        if not chunk:
            return None

        for marker in FUNGUS_MARKERS:
            start = 0
            while True:
                idx = chunk.find(marker, start)
                if idx == -1:
                    break
                name = self._name_near_offset(chunk, idx, 320)
                if name:
                    return name
                start = idx + len(marker)

        return None

    def from_dialog_hints(
        self, chunk: bytes, exclude: set[str], *, min_hits: int = 2
    ) -> str | None:
        """Match option hint lines from the same event (dialog text in RAM)."""
        if not chunk:
            return None

        scores: dict[str, int] = {}
        for name, hint in self._hint_index:
            if name.lower() in exclude:
                continue
            for pat in self._patterns(hint):
                if pat in chunk:
                    scores[name] = scores.get(name, 0) + 1
                    break

        if not scores:
            return None

        name, score = max(scores.items(), key=lambda item: (item[1], len(item[0])))
        if score >= min_hits:
            return name

        for hint_name, hint in self._hint_index:
            if len(hint) < 18 or hint_name.lower() in exclude:
                continue
            for pat in self._patterns(hint):
                if pat in chunk:
                    return hint_name
        return None

    @staticmethod
    def from_unblocked_hits(hits: list[str], exclude: set[str]) -> str | None:
        usable = [h for h in hits if h.lower() not in exclude]
        if len(usable) == 1:
            return usable[0]
        return None

    def from_new_event_name(
        self,
        hits: list[str],
        exclude: set[str],
        previous_presence: set[str],
    ) -> str | None:
        fresh = [
            h
            for h in hits
            if h.lower() not in exclude and h.lower() not in previous_presence
        ]
        if not fresh:
            return None
        if len(fresh) == 1:
            return fresh[0]
        return min(fresh, key=len)

    def predict_from_chunk(
        self,
        chunk: bytes,
        hits: list[str],
        exclude: set[str],
        previous_presence: set[str],
        *,
        force: bool = False,
    ) -> tuple[str | None, str]:
        """Returns (event_name, method_label)."""
        name = self.from_fungus_state(chunk)
        if name and name.lower() not in exclude:
            return name, "Fungus currEvent"

        min_hits = 1 if force else 2
        name = self.from_dialog_hints(chunk, exclude, min_hits=min_hits)
        if name:
            return name, "Dialog-Text"

        name = self.from_new_event_name(hits, exclude, previous_presence)
        if name:
            return name, "Event-Name (neu)"

        if force:
            name = self.from_unblocked_hits(hits, exclude)
            if name:
                return name, "RAM (einziger Treffer)"

        return None, ""
