#!/usr/bin/env python3
import sys

tokens = list(map(int, sys.stdin.read().split()))
n = tokens[0]
values = tokens[1:n + 1]
print(sum(values), max(values))
