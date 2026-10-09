# expect: refuse a conditional expression
# doc: https://docs.pymcu.org/limitations/#arithmetic
# `buf if flag else other` with both arms real buffers has no lowering yet -- the
# ternary's result type promoted both arms as scalars and copied the first BYTE of
# whichever array won, instead of CPython's bytearray object itself. Refused by name.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
other = bytearray([7])
flag = len(buf) > 1
print(head(buf if flag else other))
print("END")
