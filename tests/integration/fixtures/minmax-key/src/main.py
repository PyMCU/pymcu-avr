# minmax-key: min() and max() with a `key=` function (issue #190).
#
# `key=` used to reach the reader as `Unknown Expression type: KeywordArgExpr`, the name of a
# class inside the compiler. It compiles now: the key is called on each operand, the operands
# are compared by their keys, and what comes back is the ORIGINAL value.
#
# `rank` inverts, so the key answer is the opposite of the plain one on every line, and the
# plain calls sitting next to them are the control. `calls` counts the key calls, which is
# how a second evaluation of an operand would show up: one call per operand is what CPython
# makes, and it is what the lowering binds each operand to a name of its own to keep.
#
# Expected UART output:
#   KEY
#   1
#   3
#   3
#   1
#   70
#   7
#   END
from pymcu.hal.uart import UART
from pymcu.types import uint8

calls: uint8 = 0


def rank(x: uint8) -> uint8:
    global calls
    calls = calls + 1
    return 100 - x


uart = UART(115200)
print("KEY")
print(max(3, 1, key=rank))
print(min(3, 1, key=rank))
print(max(3, 1))
print(min(3, 1))
xs: uint8[3] = [30, 10, 70]
print(min(xs, key=rank))
print(calls)
print("END")

while True:
    pass
