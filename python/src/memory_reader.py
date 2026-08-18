"""Read Monster Prom player stats from process memory (Mono/Unity)."""

from __future__ import annotations

import json
import struct
import threading
import time
from pathlib import Path

from .event_db import STAT_KEYS

MEMORY_STAT_ORDER = STAT_KEYS

PLAYER_COLORS = {
    "Yellow": 0,
    "Green": 1,
    "Red": 2,
    "Blue": 3,
}

PROCESS_NAMES = ("Monster Prom.exe", "MonsterProm.exe", "Monster Prom")

MAX_REGION_READ = 2_000_000
MAX_PATTERN_SCAN_BYTES = 80_000_000
EVENT_SCAN_WINDOW = 8_000_000
MAX_STAT_DELTA = 3


class MemoryReaderError(Exception):
    pass


def _try_import_pymem():
    try:
        import pymem
        import pymem.process

        return pymem, pymem.process
    except ImportError as exc:
        raise MemoryReaderError(
            "pymem missing. Install with: pip install pymem"
        ) from exc


class StatMemoryReader:
    def __init__(self, cache_path: Path) -> None:
        self.cache_path = cache_path
        self.pm = None
        self.base_address: int | None = None
        self.stats_address: int | None = None
        self.player_stride: int = 24
        self.stat_stride: int = 4
        self._lock = threading.RLock()
        self._load_cache()

    def _load_cache(self) -> None:
        if not self.cache_path.exists():
            return
        try:
            data = json.loads(self.cache_path.read_text(encoding="utf-8"))
            order = data.get("stat_order")
            if order and list(order) != list(MEMORY_STAT_ORDER):
                return
            addr = data.get("stats_address")
            if isinstance(addr, int) and addr > 0:
                self.stats_address = addr
                self.player_stride = int(data.get("player_stride", 24))
                self.stat_stride = int(data.get("stat_stride", 4))
        except (json.JSONDecodeError, OSError, TypeError, ValueError):
            self.stats_address = None

    def _save_cache(self) -> None:
        self.cache_path.parent.mkdir(parents=True, exist_ok=True)
        self.cache_path.write_text(
            json.dumps(
                {
                    "stats_address": self.stats_address,
                    "stat_order": list(MEMORY_STAT_ORDER),
                    "player_stride": self.player_stride,
                    "stat_stride": self.stat_stride,
                },
                indent=2,
            ),
            encoding="utf-8",
        )

    def clear_cache(self) -> None:
        with self._lock:
            self.stats_address = None
            self.player_stride = 24
            self.stat_stride = 4
            if self.cache_path.exists():
                self.cache_path.unlink(missing_ok=True)

    def attach(self) -> bool:
        with self._lock:
            pymem, pymem_process = _try_import_pymem()
            if self.pm is not None:
                return True

            last_error = None
            for name in PROCESS_NAMES:
                try:
                    self.pm = pymem.Pymem(name)
                    mod = pymem_process.module_from_name(self.pm.process_handle, name)
                    self.base_address = mod.lpBaseOfDll
                    return True
                except Exception as exc:
                    last_error = exc
                    self.pm = None

            raise MemoryReaderError(
                "Monster Prom is not running or access was denied (try as admin?). "
                f"Error: {last_error}"
            )

    def detach(self) -> None:
        with self._lock:
            if self.pm is not None:
                try:
                    self.pm.close_process()
                except Exception:
                    pass
            self.pm = None

    @staticmethod
    def _pack_stats(stats: dict[str, int]) -> bytes:
        values = [int(stats.get(k, 0)) for k in MEMORY_STAT_ORDER]
        return struct.pack("<" + "i" * len(values), *values)

    @staticmethod
    def _unpack_stats(values: tuple[int, ...]) -> dict[str, int]:
        return dict(zip(MEMORY_STAT_ORDER, values))

    @staticmethod
    def _stats_match(read: dict[str, int] | None, expected: dict[str, int]) -> bool:
        if not read:
            return False
        return all(int(read.get(k, -1)) == int(expected.get(k, 0)) for k in STAT_KEYS)

    @staticmethod
    def _stats_plausible(read: dict[str, int], baseline: dict[str, int]) -> bool:
        if not read:
            return False
        for k in STAT_KEYS:
            if abs(int(read.get(k, 0)) - int(baseline.get(k, 0))) > MAX_STAT_DELTA:
                return False
        return True

    @staticmethod
    def _valid_stats(values: tuple[int, ...]) -> bool:
        return all(0 <= v <= 99 for v in values) and any(v > 0 for v in values)

    def _decode_block(
        self, address: int, stat_stride: int = 4
    ) -> dict[str, int] | None:
        import pymem.memory

        need = stat_stride * (len(MEMORY_STAT_ORDER) - 1) + 4
        try:
            raw = pymem.memory.read_bytes(
                self.pm.process_handle, address, need
            )
        except Exception:
            return None

        values: list[int] = []
        for i in range(len(MEMORY_STAT_ORDER)):
            off = i * stat_stride
            if off + 4 > len(raw):
                return None
            (val,) = struct.unpack_from("<i", raw, off)
            values.append(val)

        if not self._valid_stats(tuple(values)):
            return None
        return self._unpack_stats(tuple(values))

    def _decode_block_float(self, address: int) -> dict[str, int] | None:
        import pymem.memory

        try:
            raw = pymem.memory.read_bytes(
                self.pm.process_handle, address, 4 * len(MEMORY_STAT_ORDER)
            )
            floats = struct.unpack("<" + "f" * len(MEMORY_STAT_ORDER), raw)
            values = tuple(int(round(v)) for v in floats)
            if not self._valid_stats(values):
                return None
            return self._unpack_stats(values)
        except Exception:
            return None

    def _find_pattern_hits(self, pattern: bytes, limit: int = 40) -> list[int]:
        import pymem.memory

        handle = self.pm.process_handle
        hits: list[int] = []

        pymem = _try_import_pymem()[0]
        try:
            result = pymem.pattern.pattern_scan_all(
                handle, pattern, return_multiple=True
            )
            addrs = result if isinstance(result, list) else ([result] if result else [])
            hits.extend(int(a) for a in addrs[:limit])
            if hits:
                return hits
        except Exception:
            pass

        address = 0
        scanned = 0
        while address < 0x7FFFFFFF0000 and scanned < MAX_PATTERN_SCAN_BYTES:
            try:
                mbi = pymem.memory.virtual_query(handle, address)
            except Exception:
                break
            if mbi.State == 0x1000 and mbi.Protect in (0x04, 0x02, 0x20, 0x40):
                size = min(mbi.RegionSize, MAX_REGION_READ)
                try:
                    chunk = pymem.memory.read_bytes(handle, mbi.BaseAddress, size)
                    scanned += size
                    start = 0
                    while len(hits) < limit:
                        idx = chunk.find(pattern, start)
                        if idx == -1:
                            break
                        hits.append(mbi.BaseAddress + idx)
                        start = idx + 4
                except Exception:
                    pass
            address = mbi.BaseAddress + mbi.RegionSize
        return hits

    def _try_calibration_at(
        self, hit: int, expected: dict[str, int], color: str
    ) -> bool:
        idx = PLAYER_COLORS.get(color, 0)
        for player_stride in (24, 28, 32, 40, 48, 96):
            for p0_base in (
                hit,
                hit - player_stride,
                hit - 2 * player_stride,
                hit - 3 * player_stride,
            ):
                if p0_base < 0:
                    continue
                player_addr = p0_base + idx * player_stride
                for stat_stride in (4, 8):
                    decoded = self._decode_block(player_addr, stat_stride)
                    if self._stats_match(decoded, expected):
                        self.stats_address = p0_base
                        self.player_stride = player_stride
                        self.stat_stride = stat_stride
                        self._save_cache()
                        return True
                decoded = self._decode_block_float(player_addr)
                if self._stats_match(decoded, expected):
                    self.stats_address = p0_base
                    self.player_stride = player_stride
                    self.stat_stride = 4
                    self._save_cache()
                    return True
        return False

    def scan_stats_address(self, stats: dict[str, int], color: str = "Yellow") -> int | None:
        expected = {k: int(stats.get(k, 0)) for k in STAT_KEYS}
        if not any(expected.values()):
            return None

        with self._lock:
            if not self.pm:
                self.attach()

            packed = self._pack_stats(expected)
            for addr in self._find_pattern_hits(packed, limit=30):
                if self._try_calibration_at(addr, expected, color):
                    return self.stats_address

            head = struct.pack(
                "<iii",
                expected["SMARTS"],
                expected["BOLD"],
                expected["CREATIVE"],
            )
            for addr in self._find_pattern_hits(head, limit=ANCHOR_SCAN_LIMIT):
                if self._try_calibration_at(addr, expected, color):
                    return self.stats_address

        return None

    def verify_stable_reads(
        self, color: str, expected: dict[str, int], samples: int = 3, delay: float = 0.15
    ) -> bool:
        """True if memory reads match expected and are identical across samples."""
        if not self.stats_address:
            return False
        readings: list[dict[str, int]] = []
        for _ in range(samples):
            stats = self.read_player_stats(color)
            if not stats or not self._stats_match(stats, expected):
                return False
            readings.append(stats)
            time.sleep(delay)
        first = readings[0]
        return all(stats == first for stats in readings[1:])

    def read_stats_at(
        self, address: int, player_index: int = 0, stride: int | None = None
    ) -> dict[str, int] | None:
        with self._lock:
            if not self.pm:
                return None
            ps = stride or self.player_stride
            offset = player_index * ps
            addr = address + offset
            if self.stat_stride == 8:
                return self._decode_block(addr, 8)
            decoded = self._decode_block(addr, 4)
            if decoded:
                return decoded
            return self._decode_block_float(addr)

    def read_player_stats(self, color: str) -> dict[str, int] | None:
        if not self.stats_address:
            return None
        idx = PLAYER_COLORS.get(color, 0)
        return self.read_stats_at(self.stats_address, idx, self.player_stride)

    def read_stats_auto(
        self, color: str, fallback: dict[str, int] | None = None
    ) -> tuple[dict[str, int] | None, str]:
        try:
            with self._lock:
                if not self.pm:
                    self.attach()

            if self.stats_address:
                stats = self.read_player_stats(color)
                if stats and fallback and self._stats_plausible(stats, fallback):
                    return stats, ""
                if stats and not fallback:
                    return stats, ""
                self.clear_cache()

            if fallback and any(int(fallback.get(k, 0)) for k in STAT_KEYS):
                found = self.scan_stats_address(fallback, color)
                if found:
                    stats = self.read_player_stats(color)
                    if stats:
                        return stats, "Memory OK"
                return (
                    None,
                    "Stats not found in RAM — enter values exactly as shown "
                    "in-game (the game must be running).",
                )

            return None, ""

        except MemoryReaderError as exc:
            return None, str(exc)
        except Exception as exc:
            return None, f"Memory error: {exc}"

    @staticmethod
    def _string_patterns(text: str) -> list[bytes]:
        raw = text.encode("utf-8")
        wide = text.encode("utf-16-le")
        return [raw, wide]

    def _read_scan_window(self, handle, window: int = EVENT_SCAN_WINDOW) -> bytes | None:
        import pymem.memory

        if not self.stats_address:
            return None
        start = max(0, self.stats_address - window // 2)
        try:
            return pymem.memory.read_bytes(handle, start, window)
        except Exception:
            return None

    def _scan_event_hits_in_chunk(
        self, chunk: bytes, event_names: list[str], min_len: int = 4
    ) -> list[str]:
        hits: list[str] = []
        for name in event_names:
            if not (min_len <= len(name) <= 32):
                continue
            for pat in self._string_patterns(name):
                if pat in chunk:
                    hits.append(name)
                    break
        return list(dict.fromkeys(hits))

    def scan_event_hits(
        self, event_names: list[str], min_len: int = 4
    ) -> list[str]:
        """All event names visible in RAM near stats (including stale)."""
        if not self.stats_address:
            return []

        try:
            with self._lock:
                if not self.pm:
                    self.attach()
                handle = self.pm.process_handle
        except (MemoryReaderError, Exception):
            return []

        chunk = self._read_scan_window(handle)
        if not chunk:
            return []

        return self._scan_event_hits_in_chunk(chunk, event_names, min_len)

    @staticmethod
    def pick_new_event(
        hits: list[str],
        exclude: set[str],
        previous_presence: set[str],
    ) -> str | None:
        """Event that just appeared in RAM (not in last snapshot / blocklist)."""
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

    def _iter_process_chunks(self, max_bytes: int = 35_000_000):
        import pymem.memory

        handle = self.pm.process_handle
        address = 0
        total = 0
        while address < 0x7FFFFFFF0000 and total < max_bytes:
            try:
                mbi = pymem.memory.virtual_query(handle, address)
            except Exception:
                break
            if mbi.State == 0x1000 and mbi.Protect in (0x04, 0x02, 0x20, 0x40):
                size = min(mbi.RegionSize, MAX_REGION_READ)
                try:
                    yield pymem.memory.read_bytes(handle, mbi.BaseAddress, size)
                    total += size
                except Exception:
                    pass
            address = mbi.BaseAddress + mbi.RegionSize

    def scan_active_event(
        self,
        event_names: list[str],
        exclude: set[str] | None = None,
        previous_presence: set[str] | None = None,
        min_len: int = 4,
    ) -> str | None:
        if not self.stats_address:
            return None

        skip = {e.lower() for e in (exclude or set())}
        prev = previous_presence or set()
        hits = self.scan_event_hits(event_names, min_len)
        return self.pick_new_event(hits, skip, prev)
