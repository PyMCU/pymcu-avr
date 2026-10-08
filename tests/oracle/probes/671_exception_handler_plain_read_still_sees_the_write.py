# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Control case for probe 670: same shape (x declared 32, reassigned to 4,
# then read in the except handler that catches the raise), but the read
# is a plain `x + 0`, not a mod divisor. This is NOT a case the whole-
# program mod-divisor criterion touches at all -- it pins that ordinary
# constant-folding of x already answers 4 correctly here, so the fix for
# probe 670 is refusing the MOD rewrite specifically, not papering over a
# bug in how x's value is read in general. CPython: 4.
from pymcu.types import uint16


def f() -> uint16:
    x: uint16 = 32
    try:
        x = 4
        raise ValueError("x")
    except ValueError:
        return x + 0
    return 0


print(f())
print("END")
