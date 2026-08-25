# PyMCU -- ctor-calls-own-method: a constructor that calls one of its own methods.
#
# Regression for PyMCU#93. The field the method assigned read as zero, and the store landed on
# an unrelated module-level variable, which was overwritten. Marking the method @inline gave
# the right answer, so the default outline path was the one that lost the write.
#
# `guard` is here to catch the second half: a store that misses its target has to land
# somewhere, and it landed there.
#
# GPIOR0 reads 0 out of reset, so s is 7.
#
# Expected UART output:
#   77
#   111
#   7
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8

guard: uint8 = 111


class A:
    def __init__(self, a: uint8):
        self.a: uint8 = a
        self.b: uint8 = 0
        self.calc()

    def calc(self) -> None:
        self.b = 77


def main() -> None:
    s: uint8 = GPIOR0.value + 7
    o = A(s)
    print(o.b)
    print(guard)
    print(o.a)
    print("done")
