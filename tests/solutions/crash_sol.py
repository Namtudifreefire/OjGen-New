# Lời giải Python CỐ Ý LỖI với một số test (kiểm thử mã thoát khác 0 và stderr)
import sys

s = sum(int(x) for x in sys.stdin.read().split())
if s % 3 == 0:
    raise SystemExit("boom")
print(s)
