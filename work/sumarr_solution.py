import sys

tokens = list(map(int, sys.stdin.buffer.read().split()))
if not tokens:
    raise SystemExit(1)
n = tokens[0]
a = tokens[1:1 + n]
q = tokens[1 + n]
prefix = [0] * (n + 1)
for i, value in enumerate(a, 1):
    prefix[i] = prefix[i - 1] + value
pos = 2 + n
answers = []
for _ in range(q):
    x, y = tokens[pos], tokens[pos + 1]
    pos += 2
    answers.append(str(prefix[y] - prefix[x - 1]))
sys.stdout.write("\n".join(answers) + "\n")