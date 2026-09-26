# expect: match
# doc: docs/language/roadmap.md:53
# The same five constructions as the ARM of a conditional expression, a position no probe
# had used for anything but a literal or a name. The guard is runtime-seeded so the arm is
# not folded away, and the control above each line is the same value written plainly.
#
# The last pair is the sharper one: BOTH arms are method calls on different instances, so
# selecting the wrong arm prints the other object's field instead of a wrong arithmetic
# result, which a shared-slot or receiver mixup would produce.
#
# The citation is the closest the docs come: roadmap.md:53 is the only line that names a
# CONDITIONAL EXPRESSION at all, and it is written for the runtime-`str` case. The integer
# and method-call forms this probe uses have no row of their own.
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
p = Src(1 + GPIOR0.value)
print(o.get())
print(o.get() if flag else 99)
print(len(buf))
print(len(buf) if flag else 99)
print(src[idx])
print(src[idx] if flag else 99)
print(o.base + 1)
print(o.base + 1 if flag else 99)
print(p.get())
print(o.get() if flag else p.get())
print("END")
