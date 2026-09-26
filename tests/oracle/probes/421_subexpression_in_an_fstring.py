# expect: match
# doc: docs/language/limitations.md:139
# The same five constructions inside an f-string interpolation. print() dispatches on the
# SYNTACTIC SHAPE of its argument through a long ladder, so each spelling that reaches it is
# a separate branch, and a formatted value is a different branch from a bare one. No probe
# had ever put a call, a len(), a subscript or a conditional expression inside `{...}`.
#
# Every line prints the construction plainly first and interpolated second, so the two
# spellings of one value sit next to each other in one program.
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
print(o.get())
print(f"{o.get()}")
print(len(buf))
print(f"{len(buf)}")
print(src[idx])
print(f"{src[idx]}")
print(0 if flag else 9)
print(f"{0 if flag else 9}")
print(o.base + 1)
print(f"{o.base + 1}")
print("END")
