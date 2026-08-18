"""Capture Monster Prom window and read on-screen dialog via Windows OCR."""

from __future__ import annotations

import ctypes
import re
from ctypes import wintypes
from typing import Callable

user32 = ctypes.windll.user32

WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)


class RECT(ctypes.Structure):
    _fields_ = [
        ("left", ctypes.c_long),
        ("top", ctypes.c_long),
        ("right", ctypes.c_long),
        ("bottom", ctypes.c_long),
    ]


class POINT(ctypes.Structure):
    _fields_ = [("x", ctypes.c_long), ("y", ctypes.c_long)]


def _window_titles() -> list[tuple[int, str]]:
    found: list[tuple[int, str]] = []

    def callback(hwnd: int, _: int) -> bool:
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        found.append((hwnd, buf.value))
        return True

    user32.EnumWindows(WNDENUMPROC(callback), 0)
    return found


def find_game_hwnd(game_name: str = "Monster Prom") -> int | None:
    needle = game_name.strip().lower()
    if not needle:
        needle = "monster prom"
    compact = needle.replace(" ", "")

    matches: list[tuple[int, str, int]] = []
    for hwnd, title in _window_titles():
        t = title.lower()
        score = 0
        if t == needle or t == compact:
            score = 100
        elif needle in t or compact in t.replace(" ", ""):
            score = 50
        elif "monster" in t and "prom" in t:
            score = 40
        if score:
            matches.append((hwnd, title, score))
    if not matches:
        return None
    matches.sort(key=lambda item: -item[2])
    return matches[0][0]


def get_client_screen_rect(hwnd: int) -> tuple[int, int, int, int] | None:
    """Client area in screen coordinates (no window border)."""
    client = RECT()
    if not user32.GetClientRect(hwnd, ctypes.byref(client)):
        return None
    w = client.right - client.left
    h = client.bottom - client.top
    if w < 200 or h < 200:
        return None
    pt = POINT(0, 0)
    if not user32.ClientToScreen(hwnd, ctypes.byref(pt)):
        return None
    return pt.x, pt.y, w, h


def get_window_rect(hwnd: int) -> tuple[int, int, int, int] | None:
    return get_client_screen_rect(hwnd)


def dialog_region(
    left: int, top: int, width: int, height: int
) -> tuple[int, int, int, int]:
    """Lower-center band where Fungus dialogue and options usually appear."""
    x = left + int(width * 0.05)
    y = top + int(height * 0.42)
    w = int(width * 0.90)
    h = int(height * 0.55)
    return x, y, w, h


def options_region(
    left: int, top: int, width: int, height: int
) -> tuple[int, int, int, int]:
    """Bottom strip — both answer buttons."""
    x = left + int(width * 0.08)
    y = top + int(height * 0.62)
    w = int(width * 0.84)
    h = int(height * 0.36)
    return x, y, w, h


def _import_ocr() -> Callable:
    from winocr import recognize_pil_sync

    return recognize_pil_sync


def _import_capture():
    import mss
    from PIL import Image

    return mss, Image


def ocr_dependencies_ok() -> tuple[bool, str]:
    try:
        _import_capture()
        _import_ocr()
        return True, ""
    except ImportError as exc:
        return False, str(exc)


def _grab_region(left: int, top: int, width: int, height: int):
    mss_mod, Image = _import_capture()
    with mss_mod.mss() as sct:
        shot = sct.grab({"left": left, "top": top, "width": width, "height": height})
    return Image.frombytes("RGB", shot.size, shot.bgra, "raw", "BGRX")


def _capture_from_hwnd(hwnd: int, region_fn) -> object | None:
    rect = get_client_screen_rect(hwnd)
    if not rect:
        return None
    left, top, width, height = rect
    x, y, w, h = region_fn(left, top, width, height)
    return _grab_region(x, y, w, h)


def capture_window_image(game_name: str = "Monster Prom"):
    hwnd = find_game_hwnd(game_name)
    if not hwnd:
        return None
    rect = get_client_screen_rect(hwnd)
    if not rect:
        return None
    left, top, width, height = rect
    return _grab_region(left, top, width, height)


def capture_dialog_image(game_name: str = "Monster Prom"):
    hwnd = find_game_hwnd(game_name)
    if not hwnd:
        return None
    return _capture_from_hwnd(hwnd, dialog_region)


def capture_options_image(game_name: str = "Monster Prom"):
    hwnd = find_game_hwnd(game_name)
    if not hwnd:
        return None
    return _capture_from_hwnd(hwnd, options_region)


def normalize_ocr_text(raw: str) -> str:
    text = re.sub(r"\s+", " ", (raw or "").strip())
    text = re.sub(r"[^\w\s'\",.!?\-–—]", " ", text, flags=re.UNICODE)
    return re.sub(r"\s+", " ", text).strip()


def _parse_ocr_result(result) -> tuple[str, list[str]]:
    if isinstance(result, dict):
        raw = result.get("text") or ""
        line_objs = result.get("lines") or []
    else:
        raw = getattr(result, "text", "") or ""
        line_objs = getattr(result, "lines", None) or []

    lines: list[str] = []
    for line in line_objs:
        if isinstance(line, dict):
            t = line.get("text") or ""
        else:
            t = getattr(line, "text", "") or ""
        t = normalize_ocr_text(t)
        if len(t) >= 3:
            lines.append(t)
    return normalize_ocr_text(raw), lines


def _ocr_image(img, lang: str = "en") -> tuple[str, list[str]]:
    recognize = _import_ocr()
    return _parse_ocr_result(recognize(img, lang))


def guess_answer_lines(lines: list[str]) -> tuple[str, str] | None:
    """Last two short-ish OCR lines are usually the two click options."""
    candidates = [ln for ln in lines if 4 <= len(ln) <= 140]
    if len(candidates) >= 2:
        return candidates[-2], candidates[-1]
    return None


def _detail_from_image(img, source: str, lang: str = "en") -> dict:
    try:
        text, lines = _ocr_image(img, lang)
    except Exception as exc:
        err = str(exc)
        if "is_language_supported" in err or "Language" in err:
            err += " — Windows: Englisch-OCR-Paket installieren"
        return {
            "ok": False,
            "text": "",
            "lines": [],
            "option1": "",
            "option2": "",
            "source": source,
            "error": err,
        }
    pair = guess_answer_lines(lines)
    opt1, opt2 = pair if pair else ("", "")
    return {
        "ok": bool(text or lines),
        "text": text,
        "lines": lines,
        "option1": opt1,
        "option2": opt2,
        "source": source,
        "error": "" if (text or lines) else "kein Text",
    }


def _ocr_quality(detail: dict) -> int:
    if not detail.get("ok"):
        return 0
    score = len(detail.get("text") or "")
    score += len(detail.get("lines") or []) * 8
    if detail.get("option1") and detail.get("option2"):
        score += 40
    return score


def read_dialog_ocr_detail(
    game_name: str = "Monster Prom",
    lang: str = "en",
    *,
    full_window: bool = False,
) -> dict:
    """
    Screenshot + OCR with line list and guessed option 1/2.
    Tries options strip, dialog band, and full window — keeps best read.
    """
    hwnd = find_game_hwnd(game_name)
    if not hwnd:
        return {
            "ok": False,
            "text": "",
            "lines": [],
            "option1": "",
            "option2": "",
            "error": "Monster Prom window not found — is the game visible and not minimized?",
        }

    attempts: list[tuple[str, object]] = []
    if full_window:
        img = capture_window_image(game_name)
        if img:
            attempts.append(("window", img))
    for label, capturer in (
        ("choices", capture_options_image),
        ("dialog", capture_dialog_image),
        ("window", capture_window_image),
    ):
        if any(a[0] == label for a in attempts):
            continue
        img = capturer(game_name)
        if img:
            attempts.append((label, img))

    if not attempts:
        return {
            "ok": False,
            "text": "",
            "lines": [],
            "option1": "",
            "option2": "",
            "error": "Screenshot failed (window too small?)",
        }

    best: dict | None = None
    best_score = -1
    errors: list[str] = []
    for source, img in attempts:
        detail = _detail_from_image(img, source, lang)
        if detail.get("error") and not detail.get("ok"):
            errors.append(f"{source}: {detail['error']}")
        q = _ocr_quality(detail)
        if q > best_score:
            best_score = q
            best = detail

    if not best or not best.get("ok"):
        err = "; ".join(errors[:2]) if errors else "No text — are both choices fully visible?"
        return {
            "ok": False,
            "text": "",
            "lines": [],
            "option1": "",
            "option2": "",
            "error": err,
        }
    return best


def read_visible_dialog(game_name: str = "Monster Prom", lang: str = "en") -> str:
    """
    Screenshot + Windows OCR. Raises ImportError if deps missing.
    Returns empty string when no window or unreadable text.
    """
    detail = read_dialog_ocr_detail(game_name, lang)
    return detail.get("text") or ""
