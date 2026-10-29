import sys

tokens = iter(map(int, sys.stdin.buffer.read().split()))
try:
    n = next(tokens)
    q = next(tokens)
except StopIteration:
    raise SystemExit(1)

prefix = [0] * (n + 1)
for i in range(1, n + 1):
    value = next(tokens)
    prefix[i] = prefix[i - 1] + (value if value > 0 else 0)

out = []
for _ in range(q):
    left = next(tokens)
    right = next(tokens)
    out.append(str(prefix[right] - prefix[left - 1]))

sys.stdout.write("\n".join(out) + "\n")
