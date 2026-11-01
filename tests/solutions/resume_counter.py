#!/usr/bin/env python3
"""Deterministic resume fixture: succeed once, fail on invocation 2, then succeed."""
import os
import sys
from pathlib import Path

counter_path = Path(os.environ["OJGEN_TEST_RESUME_COUNTER"])
try:
    count = int(counter_path.read_text(encoding="utf-8"))
except (FileNotFoundError, ValueError):
    count = 0
count += 1
counter_path.write_text(str(count), encoding="utf-8")

if count == 2:
    raise SystemExit(1)

payload = sys.stdin.read().strip()
print(f"{count}:{payload}")
