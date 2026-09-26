# expect: refuse f-string
# doc: docs/language/limitations.md:149
# An f-string is supported streamed to a sink and assigned to a name, and refused in other
# expression positions. A conditional-expression ARM is one of those positions, and it is a
# different one from probe 102's call argument, so it gets its own probe: the refusal
# surface is per position, not per construction. The diagnostic teaches the way out
# ("Assign the f-string to a name first"), which is what makes this a limit and not a wall.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


o = Src(2 + GPIOR0.value)
flag = GPIOR0.value == 0
src = bytearray(4)
src[0] = 0
src[1] = 1
src[2] = 2
src[3] = 3
idx = 2 + GPIOR0.value
buf = bytearray(3)
tbl = [10, 11, 12, 13]
x = o.get()
print(f"a{x}" if flag else f"b{x}")
print("END")
