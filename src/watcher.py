"""Background file watcher for Monster Prom logs."""

from __future__ import annotations

import json
import time
from pathlib import Path

from .log_parser import END_ROUND_SCENES, IN_ROUND_SCENES, ParsedLine, parse_line
from .notifier import notify


class GameLogWatcher:
    def __init__(self, config: dict, project_root: Path) -> None:
        self.config = config
        self.project_root = project_root
        self.notifications = config.get("notifications", {})
        self.poll_interval = config.get("poll_interval_seconds", 0.5)
        self.skip_history = config.get("skip_history_on_start", True)
        self.only_in_round = config.get("only_notify_in_round", True)

        secret_path = project_root / "data" / "secret_endings.json"
        secret_data = json.loads(secret_path.read_text(encoding="utf-8"))
        self.secret_events = {name.lower() for name in secret_data.get("all", [])}
        self._event_to_route = {}
        for route, names in secret_data.get("by_route", {}).items():
            for name in names:
                self._event_to_route[name.lower()] = route

        self._seen_secret: set[str] = set()
        self._seen_achievements: set[str] = set()
        self._seen_interest_locks: set[str] = set()
        self._seen_plotlines: set[str] = set()
        self._position = 0
        self._partial = ""
        self._in_round = False
        self._round_active = False  # became True at least once this log session

    def _route_for_event(self, event_name: str) -> str | None:
        return self._event_to_route.get(event_name.lower())

    def _should_notify(self, parsed: ParsedLine) -> bool:
        if not self.only_in_round:
            return True
        if parsed.kind == "game_end":
            return self._round_active
        if parsed.kind == "scene_load":
            return False
        return self._in_round

    def _reset_round_state(self) -> None:
        self._in_round = False
        self._seen_interest_locks.clear()
        self._seen_plotlines.clear()

    def _handle_scene(self, parsed: ParsedLine) -> None:
        scene = parsed.scene or ""
        if scene in IN_ROUND_SCENES:
            self._in_round = True
            self._round_active = True
            self._seen_interest_locks.clear()
            self._seen_plotlines.clear()
            print(f"[Runde aktiv] {scene}")
            return
        if scene in END_ROUND_SCENES or scene in {
            "Logo",
            "Prologue_Start",
            "PopQuiz_IntroScene",
            "PopQuiz",
        }:
            if self._in_round:
                print(f"[Runde beendet] {scene}")
            self._reset_round_state()

    def _handle_parsed(self, parsed: ParsedLine) -> None:
        if parsed.kind == "scene_load":
            self._handle_scene(parsed)
            return

        if not self._should_notify(parsed):
            return

        if parsed.kind == "event_outcome" and parsed.event_name:
            name = parsed.event_name
            if self.notifications.get("event_outcome"):
                notify(
                    "Event",
                    f"{name} → {parsed.outcome or '?'}",
                )

            if name.lower() in self.secret_events and self.notifications.get(
                "secret_ending"
            ):
                key = name.lower()
                first_only = self.notifications.get("secret_ending_first_only", True)
                if first_only and key in self._seen_secret:
                    return
                self._seen_secret.add(key)
                route = self._route_for_event(name) or "?"
                notify(
                    "Secret Ending Event!",
                    f"{name} ({route}) — {parsed.outcome or 'getriggert'}",
                    duration="long",
                )

        elif parsed.kind == "achievement" and parsed.achievement:
            if not self.notifications.get("achievement"):
                return
            if parsed.achievement in self._seen_achievements:
                return
            self._seen_achievements.add(parsed.achievement)
            notify("Achievement", parsed.achievement.replace("_", " "))

        elif parsed.kind == "game_end":
            if self.notifications.get("game_end"):
                notify("Monster Prom", "Run beendet — Credits?")
            self._reset_round_state()

        elif parsed.kind == "interest_lock":
            if not self.notifications.get("interest_lock"):
                return
            key = f"{parsed.player_color}:{parsed.love_interest}".lower()
            if key in self._seen_interest_locks:
                return
            self._seen_interest_locks.add(key)
            notify(
                "Route",
                f"{parsed.player_color} → {parsed.love_interest}",
            )

        elif parsed.kind == "plotline":
            if not self.notifications.get("plotline"):
                return
            if parsed.raw in self._seen_plotlines:
                return
            self._seen_plotlines.add(parsed.raw)
            notify("Plotline", parsed.raw)

    def process_chunk(self, text: str) -> None:
        self._partial += text
        lines = self._partial.split("\n")
        self._partial = lines.pop()
        for line in lines:
            parsed = parse_line(line)
            if parsed:
                self._handle_parsed(parsed)

    def tail_file(self, log_path: Path) -> None:
        log_path = log_path.resolve()
        print(f"Watching: {log_path}")
        if self.skip_history:
            print("Alte Log-Einträge werden ignoriert (nur neue Zeilen).")
        if self.only_in_round:
            print("Benachrichtigungen nur während InGame_School (aktive Runde).")
        print("Drücke Ctrl+C zum Beenden.\n")

        if log_path.exists() and self.skip_history:
            self._position = log_path.stat().st_size

        while True:
            try:
                if not log_path.exists():
                    time.sleep(self.poll_interval)
                    continue

                size = log_path.stat().st_size
                if size < self._position:
                    # Log wurde geleert (Spiel-Neustart) — nicht alte Zeilen erneut feuern
                    self._position = size
                    self._partial = ""
                    self._reset_round_state()
                    self._round_active = False
                    print("[Log zurückgesetzt — warte auf neue Runde]")

                with log_path.open("r", encoding="utf-8", errors="replace") as f:
                    f.seek(self._position)
                    chunk = f.read()
                    self._position = f.tell()

                if chunk:
                    self.process_chunk(chunk)

            except KeyboardInterrupt:
                raise
            except Exception as exc:
                print(f"Warnung: {exc}")

            time.sleep(self.poll_interval)

    def scan_mplogs(self, mplogs_path: Path | None) -> None:
        if mplogs_path is None or not mplogs_path.is_dir():
            return
        saved_only_in_round = self.only_in_round
        self.only_in_round = False
        for log_file in sorted(mplogs_path.glob("*.txt"), key=lambda p: p.stat().st_mtime):
            try:
                content = log_file.read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for line in content.splitlines():
                parsed = parse_line(line)
                if parsed:
                    self._handle_parsed(parsed)
        self.only_in_round = saved_only_in_round
