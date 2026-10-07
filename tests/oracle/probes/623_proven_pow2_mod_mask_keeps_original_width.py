# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (case 1):
# `x % n` with x: int8 and n: uint16 proven 256. The rewrite replaces n with
# the literal mask 255 before the result type is chosen; picking that type
# from the mask's own narrow look (255 reads as a uint8) instead of from the
# ORIGINAL divisor's uint16 sign-extended the result through int8.
# rem(int8(-1), 256) is 255 in CPython, not -1.
from pymcu.types import int8, int16, uint16


def rem(x: int8) -> int16:
    n: uint16 = 256
    return x % n


print(rem(int8(-1)))
print(rem(int8(-33)))
print("END")
