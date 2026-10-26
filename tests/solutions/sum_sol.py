# Lời giải Python mẫu: in tổng các số nguyên đọc từ stdin (dùng để kiểm thử ojgen với .py)
import sys

print(sum(int(x) for x in sys.stdin.read().split()))
