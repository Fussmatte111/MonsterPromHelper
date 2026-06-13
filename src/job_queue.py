"""Small priority job queue for overlay background work."""

from __future__ import annotations

import threading
from collections import deque


class PriorityJobQueue:
    def __init__(self, max_size: int = 12) -> None:
        self._deque: deque = deque()
        self._lock = threading.Lock()
        self._event = threading.Event()
        self._max_size = max_size

    def put(self, job, *, priority: bool = False, replace_auto_dialog: bool = False) -> None:
        with self._lock:
            if replace_auto_dialog and not priority:
                self._deque = deque(
                    j
                    for j in self._deque
                    if not (len(j) >= 1 and j[0] == "dialog" and (len(j) < 3 or not j[2]))
                )
            if priority:
                self._deque.appendleft(job)
            else:
                self._deque.append(job)
            while len(self._deque) > self._max_size:
                for i in range(len(self._deque) - 1, -1, -1):
                    j = self._deque[i]
                    is_force_dialog = (
                        len(j) >= 1
                        and j[0] == "dialog"
                        and len(j) >= 3
                        and j[2]
                    )
                    if not is_force_dialog:
                        del self._deque[i]
                        break
                else:
                    self._deque.pop()
        self._event.set()

    def get(self, timeout: float = 0.3):
        if not self._event.wait(timeout):
            return None
        with self._lock:
            if not self._deque:
                self._event.clear()
                return None
            job = self._deque.popleft()
            if not self._deque:
                self._event.clear()
            return job

    def pending(self) -> int:
        with self._lock:
            return len(self._deque)

    def busy_hint(self) -> bool:
        return self.pending() > 0
