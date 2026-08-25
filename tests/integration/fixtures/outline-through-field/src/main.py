# PyMCU -- outline-through-field: @outline on a method that calls through a field.
#
# Regression for PyMCU#103. `@outline def get(self): return self.inner.get() + 2` stopped the
# build with "call to undefined function 'self_inner_get' (typo, or a missing import?)". A body
# that reaches THROUGH a field has no standalone form -- the field is another instance, not a
# number a shared body can take as a parameter -- and the outline-safety check that already
# knew this was skipped when @outline was written explicitly. The same class compiles and runs
# with the decorator removed, which is what made the message so hard to place.
#
# GPIOR0 is the seed that keeps the program off the constant folder. It reads 0 out of reset,
# so the answer is 0 + 1 + 2.
#
# Expected UART output:
#   3
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, outline


class Inner:
    def __init__(self, v: uint8):
        self.v: uint8 = v

    def get(self) -> uint8:
        return self.v + 1


class Outer:
    def __init__(self, v: uint8):
        self.inner: Inner = Inner(v)

    @outline
    def get(self) -> uint8:
        return self.inner.get() + 2


def main() -> None:
    s: uint8 = GPIOR0.value
    o = Outer(s)
    print(o.get())
    print("done")
