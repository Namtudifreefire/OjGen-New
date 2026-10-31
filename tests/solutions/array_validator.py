#!/usr/bin/env python3
import sys
from pathlib import Path

try:
    lines = Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()
    if len(lines) != 2 or len(lines[0].split()) != 1:
        raise ValueError("expected exactly two input lines")
    n = int(lines[0])
    values = [int(value) for value in lines[1].split()]
    if n < 1 or n > 10 or len(values) != n or any(value < 1 or value > 100 for value in values):
        raise ValueError("array constraints violated")
except Exception as error:
    print(error, file=sys.stderr)
    raise SystemExit(1)

raise SystemExit(0)
