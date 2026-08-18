"""Track live game context from log lines and memory hints."""

from __future__ import annotations

from collections import deque
from dataclasses import dataclass, field

from .log_parser import ParsedLine

SCENE_LABELS: dict[str, str] = {
    "InGame_School": "School (round active)",
    "MainMenu": "Main menu",
    "Credits": "Credits",
    "Gallery": "Gallery",
    "ModTool_MainMenu": "Mod-Tool",
    "ModTool_Loader": "Mod-Loader",
    "ModTool_Creator": "Mod creator",
}

# School areas often present as plain strings in Unity RAM during events.
LOCATION_STRINGS = (
    "Auditorium",
    "Bathroom",
    "Class",
    "Cafeteria",
    "Computer Lab",
    "Field",
    "Gym",
    "Library",
    "Lounge",
    "Rooftop",
    "Shop",
    "Gymnasium",
    "Pool",
    "Parking Lot",
    "Hallway",
)



@dataclass
class GameContext:
    player_color: str = "Yellow"
    scene: str = ""
    in_round: bool = False
    love_interest: str = ""
    route_partner: str = ""
    items: list[str] = field(default_factory=list)
    memory_location: str = ""
    memory_items: list[str] = field(default_factory=list)
    current_event: str = ""
    dialog_open: bool = False
    activity: deque[str] = field(default_factory=lambda: deque(maxlen=12))

    def reset_round(self) -> None:
        self.items.clear()
        self.memory_items.clear()
        self.memory_location = ""
        self.current_event = ""
        self.dialog_open = False

    def scene_label(self) -> str:
        if self.scene:
            return SCENE_LABELS.get(self.scene, self.scene)
        return "Unbekannt"

    def location_display(self) -> str:
        if self.memory_location:
            return self.memory_location
        if self.in_round:
            return "Schule (Ort unbekannt — Dialog offen?)"
        return "—"

    def dialog_partner(self) -> str:
        if self.route_partner:
            return self.route_partner
        if self.love_interest:
            return self.love_interest
        return "—"

    def items_display(self) -> str:
        merged: list[str] = []
        for name in self.items + self.memory_items:
            if name not in merged:
                merged.append(name)
        return ", ".join(merged) if merged else "—"

    def activity_display(self) -> str:
        if not self.activity:
            return "—"
        return "\n".join(self.activity)

    def push_activity(self, text: str) -> None:
        self.activity.appendleft(text)

    def apply_log(self, parsed: ParsedLine) -> None:
        if parsed.kind == "scene_load" and parsed.scene:
            self.scene = parsed.scene
            self.in_round = parsed.scene == "InGame_School"
            if not self.in_round:
                self.dialog_open = False
            self.push_activity(f"Szene: {self.scene_label()}")

        if parsed.kind == "interest_lock":
            if parsed.player_color:
                self.player_color = parsed.player_color
            if parsed.love_interest:
                self.love_interest = parsed.love_interest
                self.push_activity(f"Route: {parsed.player_color} → {parsed.love_interest}")

        if parsed.kind == "shop_purchase" and parsed.item:
            color = parsed.player_color or ""
            if not color or color == self.player_color:
                if parsed.item not in self.items:
                    self.items.append(parsed.item)
                self.push_activity(f"Shop: {parsed.item}")

        if parsed.kind == "event_outcome" and parsed.event_name:
            self.current_event = parsed.event_name
            self.dialog_open = False
            outcome = (parsed.outcome or "?").replace("Option", "Opt ")
            self.push_activity(f"Event: {parsed.event_name} → {outcome}")

        if parsed.kind == "plotline":
            self.push_activity("Plotline-Event")

        if parsed.kind == "achievement" and parsed.achievement:
            self.push_activity(f"Achievement: {parsed.achievement}")

        if parsed.kind == "game_end":
            self.push_activity("Run finished")
            self.reset_round()

    def set_live_event(self, name: str, route: str = "") -> None:
        if name and name != self.current_event:
            self.push_activity(f"Dialog: {name}")
        self.current_event = name
        self.dialog_open = bool(name)
        if route:
            self.route_partner = route
