"""Always-on-top dialog helper: live option pick before you choose."""

from __future__ import annotations

import json
import threading
import time
import tkinter as tk
from collections import deque
from pathlib import Path
from tkinter import ttk

from .dialog_capture import ocr_dependencies_ok, read_dialog_ocr_detail, read_visible_dialog
from .event_db import STAT_KEYS, EventDatabase, best_option, recommend_option
from .event_predictor import EventPredictor
from .job_queue import PriorityJobQueue
from .log_parser import IN_ROUND_SCENES, bootstrap_from_log, parse_line
from .memory_reader import MemoryReaderError, StatMemoryReader
from .paths import resolve_paths


class OverlayApp:
    def __init__(self, project_root: Path) -> None:
        self.project_root = project_root
        self.config_path = project_root / "config.json"
        self.config = self._load_config()
        self.db = EventDatabase(project_root / "data" / "events_db.json")
        self.predictor = EventPredictor(self.db)
        self.memory = StatMemoryReader(project_root / "data" / "memory_cache.json")

        self.root = tk.Tk()
        self.root.title("Monster Prom — Dialog-Helfer")
        self.root.attributes("-topmost", True)
        self.root.geometry("420x520")
        self.root.minsize(380, 460)
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)

        self._stats: dict[str, tk.IntVar] = {s: tk.IntVar(value=0) for s in STAT_KEYS}
        self._player_color = tk.StringVar(value="Yellow")
        self._current_event = tk.StringVar(value="")
        self._status = tk.StringVar(value="Monster Prom starten…")
        self._mem_status = tk.StringVar(value="")
        self._ctx_pick = tk.StringVar(value="")
        self._scene_label = tk.StringVar(value="—")

        self._in_round = False
        overlay_cfg = self.config.get("overlay", {})
        self._use_memory = tk.BooleanVar(
            value=bool(overlay_cfg.get("auto_memory_stats", False))
        )
        self._auto_event_scan = bool(overlay_cfg.get("auto_event_scan", True))
        self._predict_dialog = bool(overlay_cfg.get("predict_dialog", True))
        self._dialog_ocr = bool(overlay_cfg.get("dialog_ocr", True))
        self._ocr_interval_s = float(overlay_cfg.get("dialog_ocr_seconds", 2.5))
        self._game_title = str(self.config.get("game", "Monster Prom"))
        self._last_ocr_text = ""
        self._ocr_ready, self._ocr_hint = ocr_dependencies_ok()
        self._memory_calibrated = False
        self._memory_trusted = False
        self._stats_baseline: dict[str, int] = {}
        self._live_dialog_event = ""
        self._event_names = self.db.all_names()
        self._finished_recent: deque[str] = deque(maxlen=24)
        self._ram_blocked: set[str] = set()
        self._manual_event_lock = False
        self._ram_presence_prev: set[str] = set()

        self._dialog_scan_ms = int(
            float(overlay_cfg.get("event_scan_seconds", 0.15)) * 1000
        )
        self._stats_refresh_ms = int(
            float(self.config.get("stats_refresh_seconds", 3.0)) * 1000
        )
        self._log_poll_ms = int(
            float(self.config.get("poll_interval_seconds", 0.2)) * 1000
        )

        self._jobs = PriorityJobQueue()
        self._mem_busy = False
        self._last_auto_dialog_enqueue = 0.0
        self._closing = False
        self._updating_from_memory = False
        self._calibrate_timer: str | None = None
        self._search_timer: str | None = None
        self._screenshot_busy = False

        if self.memory.stats_address:
            self._memory_calibrated = True

        self._build_ui()
        self._start_log_thread()
        self._start_memory_worker()

    def _load_config(self) -> dict:
        if self.config_path.exists():
            return json.loads(self.config_path.read_text(encoding="utf-8"))
        example = self.project_root / "config.example.json"
        return json.loads(example.read_text(encoding="utf-8"))

    def _on_close(self) -> None:
        self._closing = True
        self._jobs.put(("__stop__",), priority=True)
        self.memory.detach()
        self.root.destroy()

    def _build_ui(self) -> None:
        pad = {"padx": 8, "pady": 4}
        top = ttk.Frame(self.root)
        top.pack(fill=tk.X, **pad)
        ttk.Label(top, textvariable=self._status, wraplength=390).pack(anchor=tk.W)
        ttk.Label(top, textvariable=self._scene_label, wraplength=390).pack(anchor=tk.W)
        ttk.Label(top, textvariable=self._mem_status, wraplength=390).pack(anchor=tk.W)
        pick_lbl = ttk.Label(
            top,
            textvariable=self._ctx_pick,
            wraplength=390,
            font=("Segoe UI", 11, "bold"),
            foreground="#006600",
        )
        pick_lbl.pack(anchor=tk.W, pady=(4, 0))

        stats_frame = ttk.LabelFrame(self.root, text="Stats (für Empfehlung)")
        stats_frame.pack(fill=tk.X, **pad)
        mem_row = ttk.Frame(stats_frame)
        mem_row.pack(fill=tk.X, padx=6, pady=2)
        ttk.Checkbutton(
            mem_row,
            text="Stats live (experimentell)",
            variable=self._use_memory,
            command=self._on_memory_toggle,
        ).pack(side=tk.LEFT)
        ttk.Button(mem_row, text="Kalibrieren", command=self._request_calibrate).pack(
            side=tk.LEFT, padx=4
        )
        ttk.Button(mem_row, text="Cache leeren", command=self._clear_memory_cache).pack(
            side=tk.LEFT, padx=2
        )

        grid = ttk.Frame(stats_frame)
        grid.pack(padx=6, pady=4)
        for i, stat in enumerate(STAT_KEYS):
            ttk.Label(grid, text=stat, width=9).grid(
                row=i // 2, column=(i % 2) * 2, sticky=tk.W
            )
            spin = ttk.Spinbox(
                grid, from_=0, to=99, width=5, textvariable=self._stats[stat]
            )
            spin.grid(row=i // 2, column=(i % 2) * 2 + 1, padx=4, pady=1)
            spin.bind("<ButtonRelease-1>", lambda e: self._on_stats_input())
            spin.bind("<KeyRelease>", lambda e: self._on_stats_input())

        ttk.Label(stats_frame, text="Farbe:").pack(anchor=tk.W, padx=6)
        color_cb = ttk.Combobox(
            stats_frame,
            textvariable=self._player_color,
            values=["Yellow", "Green", "Red", "Blue"],
            width=10,
        )
        color_cb.pack(anchor=tk.W, padx=6, pady=(0, 4))
        color_cb.bind("<<ComboboxSelected>>", lambda e: self._request_stats_only())

        search_frame = ttk.LabelFrame(self.root, text="Dialog finden")
        search_frame.pack(fill=tk.X, padx=8, pady=2)

        scan_row = ttk.Frame(search_frame)
        scan_row.pack(fill=tk.X, padx=6, pady=(6, 2))
        self._screenshot_btn = ttk.Button(
            scan_row,
            text="Screenshot → Event (beide Antworten)",
            command=self._on_screenshot_scan,
        )
        self._screenshot_btn.pack(side=tk.LEFT, padx=(0, 4))
        if not self._ocr_ready:
            self._screenshot_btn.state(["disabled"])
        ttk.Button(
            scan_row,
            text="RAM aktualisieren",
            command=self._on_ram_refresh,
        ).pack(side=tk.LEFT, padx=(0, 4))
        ttk.Button(
            scan_row,
            text="Blockliste leeren",
            command=self._clear_ram_blocklist,
        ).pack(side=tk.LEFT)

        manual = ttk.Frame(search_frame)
        manual.pack(fill=tk.X, padx=6, pady=4)
        ttk.Label(manual, text="Suchbegriff:").pack(side=tk.LEFT)
        self._search_var = tk.StringVar()
        self._search_entry = ttk.Entry(manual, textvariable=self._search_var, width=22)
        self._search_entry.pack(side=tk.LEFT, padx=4)
        self._search_entry.bind("<Return>", lambda e: self._apply_search())
        self._search_var.trace_add("write", lambda *_: self._schedule_search())
        ttk.Button(manual, text="Suchen", command=self._apply_search).pack(side=tk.LEFT, padx=2)
        ttk.Button(manual, text="Löschen", command=self._clear_live_dialog).pack(
            side=tk.LEFT, padx=2
        )
        ttk.Button(
            manual,
            text="RAM falsch",
            command=self._block_current_ram_event,
        ).pack(side=tk.LEFT, padx=2)

        pick_row = ttk.Frame(search_frame)
        pick_row.pack(fill=tk.X, padx=6, pady=(0, 4))
        ttk.Label(pick_row, text="Treffer:").pack(side=tk.LEFT)
        self._search_pick = tk.StringVar()
        self._search_pick_cb = ttk.Combobox(
            pick_row, textvariable=self._search_pick, width=34
        )
        self._search_pick_cb.pack(side=tk.LEFT, padx=4, fill=tk.X, expand=True)
        self._search_pick_cb.bind("<<ComboboxSelected>>", lambda e: self._apply_search_pick())
        self._search_hits: list[dict] = []

        self._options_frame = ttk.LabelFrame(self.root, text="Optionen")
        self._options_frame.pack(fill=tk.BOTH, expand=True, **pad)
        self._options_text = tk.Text(
            self._options_frame, height=16, wrap=tk.WORD, state=tk.DISABLED
        )
        self._options_text.pack(fill=tk.BOTH, expand=True, padx=6, pady=6)

        ocr_note = (
            "Button: Screenshot wenn beide Antworten sichtbar sind."
            if self._ocr_ready
            else "Screenshot-Scan: pip install -r requirements.txt"
        )
        auto_note = (
            " + Auto-OCR im Hintergrund."
            if self._dialog_ocr and self._ocr_ready
            else ""
        )
        ttk.Label(
            self.root,
            text=ocr_note + auto_note + " Optional: manuelle Suche.",
            wraplength=390,
            foreground="#555",
        ).pack(fill=tk.X, padx=8, pady=4)

    def _snapshot_ui(self) -> dict:
        stats = {}
        for s in STAT_KEYS:
            try:
                stats[s] = int(self._stats[s].get())
            except (tk.TclError, ValueError):
                stats[s] = 0
        return {
            "stats": stats,
            "color": self._player_color.get(),
            "use_memory": bool(self._use_memory.get()),
            "in_round": self._in_round,
            "calibrated": self._memory_calibrated,
        }

    def _set_stats_ui(self, stats: dict[str, int]) -> None:
        self._updating_from_memory = True
        try:
            for s in STAT_KEYS:
                if s in stats:
                    self._stats[s].set(int(stats[s]))
        finally:
            self._updating_from_memory = False

    def _on_memory_toggle(self) -> None:
        if not self._use_memory.get():
            self._memory_trusted = False
            self._mem_status.set("Live-Memory aus — Stats bleiben manuell")

    def _on_stats_input(self) -> None:
        self._stats_baseline = self._snapshot_ui()["stats"]
        self._memory_trusted = False
        self._refresh_options()
        if self._updating_from_memory or not self._use_memory.get():
            return
        if self._memory_trusted:
            return
        snap = self._snapshot_ui()
        if not any(snap["stats"].values()):
            return
        if self._calibrate_timer:
            self.root.after_cancel(self._calibrate_timer)
        self._calibrate_timer = self.root.after(1200, self._auto_calibrate)

    def _auto_calibrate(self) -> None:
        self._calibrate_timer = None
        if self._memory_calibrated or not self._use_memory.get():
            return
        self._mem_status.set("Kalibriere…")
        self._enqueue(("calibrate", self._snapshot_ui()))

    def _snapshot_ram_presence(self) -> None:
        if not self.memory.stats_address:
            return
        try:
            hits = self.memory.scan_event_hits(self._event_names)
            self._ram_presence_prev = {h.lower() for h in hits}
        except (MemoryReaderError, Exception):
            pass

    def _clear_ram_blocklist(self) -> None:
        blocked_n = len(self._ram_blocked)
        finished_n = len(self._finished_recent)
        self._ram_blocked.clear()
        self._finished_recent.clear()
        self._ram_presence_prev = set()
        parts = []
        if blocked_n:
            parts.append(f"{blocked_n} blockiert")
        if finished_n:
            parts.append(f"{finished_n} fertig (Log)")
        summary = ", ".join(parts) if parts else "war leer"
        self._mem_status.set(f"RAM-Blockliste geleert ({summary})")
        self._status.set("Blockliste leer — „RAM aktualisieren“ oder Screenshot probieren")

    def _block_current_ram_event(self) -> None:
        name = self._live_dialog_event or self._current_event.get()
        if name:
            self._ram_blocked.add(name.lower())
            self._manual_event_lock = True
            self._status.set(f"RAM ignoriert: {name} — anderes Event eintippen")
            self._mem_status.set(f"'{name}' blockiert")

    def _clear_live_dialog(self) -> None:
        if self._live_dialog_event:
            self._ram_blocked.add(self._live_dialog_event.lower())
        self._live_dialog_event = ""
        self._current_event.set("")
        self._ctx_pick.set("")
        self._manual_event_lock = False
        self._search_var.set("")
        self._refresh_options()

    def _mark_event_finished(self, name: str) -> None:
        key = name.lower()
        self._finished_recent.append(key)
        self._ram_blocked.add(key)
        self._manual_event_lock = False
        self._current_event.set(name)
        self._live_dialog_event = name
        self.root.after(200, self._snapshot_ram_presence)

    def _enqueue(self, job: tuple, *, priority: bool = False) -> None:
        replace = job[0] == "dialog" and not priority and (len(job) < 3 or not job[2])
        self._jobs.put(job, priority=priority, replace_auto_dialog=replace)

    def _apply_memory_result(self, result: dict) -> None:
        if result.get("stats") and self._memory_trusted:
            self._set_stats_ui(result["stats"])
            self._refresh_options()
        if result.get("mem_status"):
            self._mem_status.set(result["mem_status"])
        if result.get("calibrated"):
            self._memory_calibrated = True
            self._memory_trusted = True
            self._stats_baseline = self._snapshot_ui()["stats"]
            if self._in_round:
                self._request_dialog_scan()

        if "event" in result:
            force = bool(result.get("force_refresh"))
            if self._manual_event_lock and not force:
                return
            found = result["event"]
            if found:
                if force:
                    self._manual_event_lock = False
                self._live_dialog_event = found
                self._current_event.set(found)
                self._search_var.set(found)
                method = result.get("predict_method", "RAM")
                label = "RAM-Refresh" if force else "Dialog"
                self._status.set(f"★ {label} ({method}): {found}")
                self._refresh_options()
            elif force:
                self._mem_status.set(
                    result.get("mem_status") or "RAM-Refresh: kein Event gefunden"
                )
                candidates = result.get("ram_candidates") or []
                if candidates:
                    self._populate_ram_candidates(candidates)

    def _clear_memory_cache(self) -> None:
        self.memory.clear_cache()
        self._memory_calibrated = False
        self._memory_trusted = False
        self._clear_live_dialog()
        self._mem_status.set("Cache leer — Stats eintragen, dann Kalibrieren")

    def _request_calibrate(self) -> None:
        snap = self._snapshot_ui()
        if not any(snap["stats"].values()):
            self._mem_status.set("Stats aus dem Spiel eintragen")
            return
        self._mem_status.set("Suche Memory…")
        self._enqueue(("calibrate", snap))

    def _request_stats_only(self) -> None:
        if (
            not self._use_memory.get()
            or not self._memory_trusted
            or not self._memory_calibrated
        ):
            return
        self._enqueue(("stats", self._snapshot_ui()))

    def _can_scan_dialog(self) -> bool:
        return (
            self._predict_dialog
            and self._in_round
            and bool(self.memory.stats_address)
        )

    def _request_dialog_scan(self, *, force: bool = False) -> None:
        if not self._predict_dialog:
            return
        if not self.memory.stats_address:
            if force:
                self._mem_status.set("Zuerst Stats kalibrieren (Kalibrieren-Button)")
            return
        if not force and not self._in_round:
            return
        if not force:
            now = time.time()
            if self._mem_busy or now - self._last_auto_dialog_enqueue < 0.45:
                return
            if self._jobs.pending() > 2:
                return
            self._last_auto_dialog_enqueue = now
        snap = self._snapshot_ui()
        self._enqueue(("dialog", snap, force), priority=force)

    def _on_ram_refresh(self) -> None:
        if not self.memory.stats_address:
            self._status.set("RAM-Refresh: Stats-Adresse fehlt")
            self._mem_status.set("Zuerst kalibrieren — Spiel läuft? Overlay als Admin?")
            return
        self._status.set("Lese Event aus RAM…")
        self._mem_status.set("RAM-Refresh (Priorität)…")
        try:
            self.memory.attach()
        except MemoryReaderError as exc:
            self._mem_status.set(str(exc))
            return
        except Exception as exc:
            self._mem_status.set(f"RAM: {exc}")
            return
        self._request_dialog_scan(force=True)

    def _memory_work(self, job: tuple) -> dict:
        force_refresh = False
        if len(job) >= 3:
            kind, snap, force_refresh = job[0], job[1], bool(job[2])
        else:
            kind, snap = job[0], job[1]
        result: dict = {}

        try:
            if kind == "calibrate":
                self.memory.attach()
                self.memory.clear_cache()
                addr = self.memory.scan_stats_address(snap["stats"], snap["color"])
                if addr and self.memory.verify_stable_reads(
                    snap["color"], snap["stats"]
                ):
                    result["calibrated"] = True
                    result["mem_status"] = "Memory verifiziert (stabil)"
                    result["stats"] = snap["stats"]
                else:
                    self.memory.clear_cache()
                    result["mem_status"] = (
                        "Stats nicht im RAM — exakt aus dem Spiel eintragen "
                        "(Spiel läuft? Admin?). Optionen: Event manuell."
                    )
                return result

            if kind == "stats" and snap["use_memory"]:
                baseline = self._stats_baseline or snap["stats"]
                stats, msg = self.memory.read_stats_auto(snap["color"], baseline)
                if stats and self.memory._stats_plausible(stats, baseline):
                    result["stats"] = stats
                elif msg:
                    result["mem_status"] = msg
                    self._memory_trusted = False
                return result

            if kind == "dialog":
                if force_refresh:
                    skip = {e.lower() for e in self._ram_blocked}
                else:
                    skip = {e.lower() for e in self._finished_recent} | self._ram_blocked
                try:
                    with self.memory._lock:
                        if not self.memory.pm:
                            self.memory.attach()
                        handle = self.memory.pm.process_handle
                except (MemoryReaderError, Exception) as exc:
                    result["mem_status"] = str(exc)
                    result["force_refresh"] = force_refresh
                    result["event"] = ""
                    return result

                chunk = self.memory._read_scan_window(handle)
                hits = self.memory._scan_event_hits_in_chunk(
                    chunk or b"", self._event_names
                )
                found, method = self.predictor.predict_from_chunk(
                    chunk or b"",
                    hits,
                    skip,
                    self._ram_presence_prev,
                    force=force_refresh,
                )

                scan_cap = 8_000_000 if force_refresh else 30_000_000
                if not found:
                    for proc_chunk in self.memory._iter_process_chunks(scan_cap):
                        found = self.predictor.from_fungus_state(proc_chunk)
                        if found and found.lower() not in skip:
                            method = "Fungus (live)"
                            break

                self._ram_presence_prev = {h.lower() for h in hits}
                result["force_refresh"] = force_refresh
                usable = [h for h in hits if h.lower() not in skip]
                if found:
                    result["event"] = found
                    result["predict_method"] = method
                    if force_refresh:
                        result["mem_status"] = f"RAM: {found} ({method})"
                else:
                    result["event"] = ""
                    if force_refresh:
                        if usable:
                            result["ram_candidates"] = usable[:15]
                            preview = ", ".join(usable[:5])
                            extra = f" (+{len(usable) - 5})" if len(usable) > 5 else ""
                            result["mem_status"] = (
                                f"RAM: {len(usable)} Kandidaten — Treffer wählen: "
                                f"{preview}{extra}"
                            )
                        else:
                            blocked = ", ".join(sorted(self._ram_blocked)[:4])
                            hint = (
                                f" Nur blockierte im RAM: {blocked}"
                                if blocked
                                else " Kein Event-Name im RAM sichtbar"
                            )
                            result["mem_status"] = (
                                "RAM-Refresh: kein Treffer." + hint
                                + " → Blockliste leeren oder Screenshot"
                            )
                return result

        except MemoryReaderError as exc:
            result["mem_status"] = str(exc)
        except Exception as exc:
            result["mem_status"] = f"Fehler: {exc}"

        return result

    def _populate_ram_candidates(self, names: list[str]) -> None:
        hits = [self.db.get(n) or {"name": n} for n in names]
        hits = [h for h in hits if h]
        if not hits:
            return
        self._search_hits = hits
        ev_names = [h.get("name", "") for h in hits]
        self._search_pick_cb["values"] = ev_names
        self._search_pick.set(ev_names[0])
        self._manual_event_lock = True
        self._current_event.set(ev_names[0])
        self._live_dialog_event = ev_names[0]
        self._refresh_options()

    def _start_memory_worker(self) -> None:
        def worker() -> None:
            while not self._closing:
                job = self._jobs.get(timeout=0.35)
                if job is None:
                    continue
                if job[0] == "__stop__":
                    break
                self._mem_busy = True
                try:
                    result = self._memory_work(job)
                except Exception as exc:
                    result = {"mem_status": f"Fehler: {exc}"}
                finally:
                    self._mem_busy = False
                if result and not self._closing:
                    self.root.after(0, lambda r=result: self._apply_memory_result(r))

        threading.Thread(target=worker, daemon=True).start()

        def stats_loop() -> None:
            while not self._closing:
                try:
                    self._request_stats_only()
                except tk.TclError:
                    break
                time.sleep(self._stats_refresh_ms / 1000.0)

        def dialog_loop() -> None:
            while not self._closing:
                try:
                    self._request_dialog_scan()
                except tk.TclError:
                    break
                time.sleep(self._dialog_scan_ms / 1000.0)

        threading.Thread(target=stats_loop, daemon=True).start()
        threading.Thread(target=dialog_loop, daemon=True).start()
        if self._dialog_ocr and self._ocr_ready:
            threading.Thread(target=self._ocr_loop, daemon=True).start()
        elif self._dialog_ocr and not self._ocr_ready:
            self._mem_status.set(
                "OCR fehlt: pip install mss Pillow winocr — dann Overlay neu starten"
            )

    def _ocr_loop(self) -> None:
        while not self._closing:
            try:
                if (
                    self._in_round
                    and not self._manual_event_lock
                    and self._dialog_ocr
                ):
                    text = read_visible_dialog(self._game_title)
                    if len(text) >= 12 and text != self._last_ocr_text:
                        self._last_ocr_text = text
                        best, hits = self.db.best_dialog_match(text)
                        if best:
                            name = best.get("name", "")
                            snippet = text[:72] + ("…" if len(text) > 72 else "")
                            payload = (name, snippet, hits)
                            self.root.after(0, lambda p=payload: self._apply_ocr_match(*p))
                        elif hits:
                            names = [h.get("name", "") for h in hits[:6]]
                            snippet = text[:60] + ("…" if len(text) > 60 else "")
                            self.root.after(
                                0,
                                lambda s=snippet, n=names: self._ocr_ambiguous(s, n),
                            )
            except Exception as exc:
                if not self._closing:
                    self.root.after(
                        0, lambda e=str(exc): self._mem_status.set(f"OCR: {e[:80]}")
                    )
            time.sleep(max(1.5, self._ocr_interval_s))

    def _apply_ocr_match(self, name: str, snippet: str, hits: list[dict]) -> None:
        if self._manual_event_lock or not name:
            return
        self._live_dialog_event = name
        self._current_event.set(name)
        self._search_var.set(snippet)
        names = [h.get("name", "") for h in hits]
        self._search_hits = hits
        self._search_pick_cb["values"] = names
        self._search_pick.set(name)
        self._status.set(f"★ Dialog (OCR): {name}")
        self._mem_status.set(f"Gelesen: {snippet}")
        self._refresh_options()

    def _ocr_ambiguous(self, snippet: str, names: list[str]) -> None:
        if self._manual_event_lock:
            return
        self._mem_status.set(f"OCR unsicher — Treffer prüfen: {', '.join(names[:3])}")
        if not self._current_event.get():
            self._search_var.set(snippet)
            self._search_pick_cb["values"] = names
            if names:
                self._search_pick.set(names[0])

    def _on_screenshot_scan(self) -> None:
        if self._screenshot_busy or not self._ocr_ready:
            return
        self._screenshot_busy = True
        try:
            self._screenshot_btn.state(["disabled"])
        except tk.TclError:
            pass
        self._mem_status.set("Screenshot + OCR…")
        self._status.set("Lese beide Antworten aus dem Spielfenster…")

        def work() -> dict:
            detail = read_dialog_ocr_detail(
                self._game_title, full_window=True
            )
            if not detail.get("ok"):
                return {"error": detail.get("error") or "OCR fehlgeschlagen"}

            best, hits, status, hint = self.db.match_screenshot(
                detail.get("text") or "",
                detail.get("option1") or "",
                detail.get("option2") or "",
            )
            return {
                "detail": detail,
                "best": best,
                "hits": hits,
                "status": status,
                "match_hint": hint,
            }

        def done(result: dict) -> None:
            self._screenshot_busy = False
            try:
                self._screenshot_btn.state(["!disabled"])
            except tk.TclError:
                pass
            if self._closing:
                return
            self._apply_screenshot_result(result)

        def runner() -> None:
            try:
                result = work()
            except Exception as exc:
                result = {"error": str(exc)}
            if not self._closing:
                self.root.after(0, lambda r=result: done(r))

        threading.Thread(target=runner, daemon=True).start()

    def _apply_screenshot_result(self, result: dict) -> None:
        if result.get("error"):
            self._status.set("Screenshot: Problem")
            self._mem_status.set(result["error"][:200])
            return

        detail = result.get("detail") or {}
        opt1 = detail.get("option1") or ""
        opt2 = detail.get("option2") or ""
        src = detail.get("source") or "?"
        read_note = f"OCR ({src})"
        if opt1 or opt2:
            read_note += f" — 1: {opt1[:45]} | 2: {opt2[:45]}"
        elif detail.get("text"):
            read_note += f" — Text: {(detail.get('text') or '')[:70]}"
        self._mem_status.set(read_note)

        hits = result.get("hits") or []
        best = result.get("best")
        status = result.get("status")
        match_hint = result.get("match_hint") or ""

        if best and status == "ok":
            name = best.get("name", "")
            self._set_active_event(name, "Screenshot")
            extra = f" ({match_hint})" if match_hint else ""
            self._status.set(f"★ Event aus Antworten: {name}{extra}")
            return

        if hits:
            names = [h.get("name", "") for h in hits]
            self._search_hits = hits
            self._search_pick_cb["values"] = names
            self._search_pick.set(names[0])
            self._manual_event_lock = True
            self._current_event.set(names[0])
            self._live_dialog_event = names[0]
            self._status.set(
                f"Screenshot: {len(hits)} Treffer — Liste prüfen"
            )
            self._refresh_options()
            return

        self._status.set("Screenshot: kein DB-Treffer")
        if match_hint:
            self._mem_status.set(f"{read_note} | {match_hint}"[:200])

    def _schedule_search(self) -> None:
        if self._search_timer:
            self.root.after_cancel(self._search_timer)
        self._search_timer = self.root.after(350, self._apply_search)

    def _set_active_event(self, name: str, source: str) -> None:
        self._manual_event_lock = True
        self._live_dialog_event = name
        self._current_event.set(name)
        self._status.set(f"★ {source}: {name}")
        self._refresh_options()

    def _apply_search_pick(self) -> None:
        name = self._search_pick.get().strip()
        if name:
            self._set_active_event(name, "Auswahl")

    def _apply_search(self) -> None:
        self._search_timer = None
        q = self._search_var.get().strip()
        if not q:
            return

        hits = self.db.search_dialog(q, limit=12)
        if not hits:
            hits = self.db.search(q, limit=8)

        self._search_hits = hits
        if not hits:
            self._status.set("Kein Treffer — andere Wörter aus dem Dialog versuchen")
            return

        names = [h.get("name", "") for h in hits]
        self._search_pick_cb["values"] = names
        self._search_pick.set(names[0])

        if len(hits) == 1:
            self._set_active_event(names[0], "Suche")
        else:
            self._set_active_event(names[0], f"Suche (1/{len(hits)} — Treffer prüfen)")

    def _focus_event_entry(self) -> None:
        try:
            self._search_entry.focus_set()
        except tk.TclError:
            pass

    def _apply_bootstrap(self, state: dict) -> None:
        self._in_round = bool(state.get("in_round"))
        scene = state.get("scene") or ""
        labels = {
            "InGame_School": "In der Runde",
            "MainMenu": "Hauptmenü",
            "Credits": "Credits",
            "Gallery": "Galerie",
            "PopQuiz": "Pop Quiz",
        }
        if scene:
            self._scene_label.set(labels.get(scene, scene))
        for ev in state.get("finished_events") or ():
            self._finished_recent.append(ev)
            self._ram_blocked.add(ev)
        color = state.get("player_color")
        if color:
            self._player_color.set(color)
        if self._in_round:
            if self._dialog_ocr and self._ocr_ready:
                self._status.set("Dialog offen → OCR liest Text automatisch aus dem Spiel")
            else:
                self._status.set("Bei Dialog: Wörter aus dem Text eintippen (siehe Suche)")
                self._focus_event_entry()
            if self.memory.stats_address:
                self.root.after(200, self._snapshot_ram_presence)
                self.root.after(400, self._request_dialog_scan)
        else:
            self._status.set("Keine aktive Runde im Log — spiele Woche oder Event manuell")
        self._refresh_options()

    def _refresh_options(self) -> None:
        name = self._current_event.get().strip()
        ev = self.db.get(name) if name else None
        self._options_text.config(state=tk.NORMAL)
        self._options_text.delete("1.0", tk.END)

        if not self._in_round and not ev:
            self._ctx_pick.set("")
            self._options_text.insert(
                tk.END,
                "Keine Runde im Log erkannt.\n"
                "Starte eine Woche — oder trage ein Event manuell ein.\n"
                "(Overlay liest beim Start den bisherigen Log mit.)",
            )
            self._options_text.config(state=tk.DISABLED)
            return

        if not ev:
            self._ctx_pick.set("")
            self._options_text.insert(
                tk.END,
                "Dialog offen?\n\n"
                "1) Wörter aus dem Dialog oben eintippen\n"
                "   (Text von Option 1/2, oder „Damien“, „party“, …)\n"
                "2) Mehrere Treffer? → richtiges Event in der Liste wählen\n"
                "3) Oder kurz warten — Auto-Erkennung (RAM)\n\n"
                "Den internen Event-Namen siehst du im Spiel nicht.",
            )
            self._options_text.config(state=tk.DISABLED)
            return

        stats = self._snapshot_ui()["stats"]
        annotated = recommend_option(ev, stats)
        pick = best_option(annotated)
        route = ev.get("route") or "?"

        if pick and pick.get("verdict") == "success":
            hint = f' — „{pick["hint"]}"' if pick.get("hint") else ""
            self._ctx_pick.set(
                f"★ OPTION {pick['option']}: {pick['stat']} "
                f"({pick['value']}){hint}"
            )
        elif pick:
            self._ctx_pick.set(
                f"Option {pick['option']} ({pick['stat']}) — knapp, Wert {pick['value']}"
            )
        else:
            self._ctx_pick.set("")

        lines = [
            f"{ev.get('name')}  |  {route}",
            "",
        ]
        if pick:
            lines.append(f">>> WÄHLE OPTION {pick['option']} <<<")
            lines.append(f"    {pick['stat']} — Wert {pick.get('value', '?')}")
            if pick.get("hint"):
                lines.append(f"    „{pick['hint']}“")
            lines.append("")

        for o in annotated:
            mark = " ★" if o.get("recommended") else ""
            verdict = o.get("verdict", "?")
            emoji = {"success": "✓", "fail": "✗", "tie": "~"}.get(verdict, "?")
            lines.append(
                f"Option {o['option']}: {o['stat']} ({o['value']}) {emoji}{mark}"
            )
            if o.get("hint"):
                lines.append(f"   „{o['hint']}“")
            lines.append("")

        self._options_text.insert(tk.END, "\n".join(lines))
        self._options_text.config(state=tk.DISABLED)

    def _on_log_line(self, line: str) -> None:
        parsed = parse_line(line)
        if not parsed:
            return

        def update() -> None:
            if parsed.kind == "scene_load" and parsed.scene:
                in_school = parsed.scene in IN_ROUND_SCENES
                self._in_round = in_school
                labels = {
                    "InGame_School": "In der Runde",
                    "MainMenu": "Hauptmenü",
                    "Credits": "Credits",
                    "Gallery": "Galerie",
                }
                self._scene_label.set(labels.get(parsed.scene, parsed.scene))
                if in_school:
                    self._status.set("Bei Dialog: Wörter aus dem Spieltext in „Dialog finden“")
                    self._focus_event_entry()
                    if self.memory.stats_address:
                        self._snapshot_ram_presence()
                        self._request_dialog_scan()
                else:
                    self._clear_live_dialog()
                    self._status.set("Menü / Pause")
                    self._mem_status.set("")

            if parsed.kind == "event_outcome" and parsed.event_name:
                if self._scene_label.get() == "In der Runde" or self._in_round:
                    self._in_round = True
                outcome = (parsed.outcome or "?").replace("Option", "Opt ")
                self._mark_event_finished(parsed.event_name)
                self._search_var.set(parsed.event_name)
                self._status.set(f"Fertig: {parsed.event_name} → {outcome}")
                self._refresh_options()
                if self.memory.stats_address:
                    self.root.after(300, self._snapshot_ram_presence)

            if parsed.kind == "interest_lock" and parsed.player_color:
                self._player_color.set(parsed.player_color)

            self._refresh_options()

        self.root.after(0, update)

    def _start_log_thread(self) -> None:
        def run() -> None:
            try:
                log_path, _ = resolve_paths(self.config)
            except FileNotFoundError as exc:
                self.root.after(0, lambda: self._status.set(str(exc)))
                return

            boot = bootstrap_from_log(log_path)
            self.root.after(0, lambda s=boot: self._apply_bootstrap(s))

            pos = log_path.stat().st_size if log_path.exists() else 0
            partial = ""
            interval = self._log_poll_ms / 1000.0

            while not self._closing:
                try:
                    if not log_path.exists():
                        time.sleep(interval)
                        continue
                    size = log_path.stat().st_size
                    if size < pos:
                        pos = 0
                        partial = ""
                        def reset_session() -> None:
                            self._finished_recent.clear()
                            self._ram_blocked.clear()
                            self._clear_live_dialog()

                        self.root.after(0, reset_session)
                    with log_path.open("r", encoding="utf-8", errors="replace") as f:
                        f.seek(pos)
                        chunk = f.read()
                        pos = f.tell()
                    if chunk:
                        partial += chunk
                        lines = partial.split("\n")
                        partial = lines.pop()
                        for line in lines:
                            if self._closing:
                                return
                            self._on_log_line(line)
                except Exception:
                    pass
                time.sleep(interval)

        threading.Thread(target=run, daemon=True).start()

    def run(self) -> None:
        self.root.mainloop()


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    OverlayApp(root).run()
    return 0
