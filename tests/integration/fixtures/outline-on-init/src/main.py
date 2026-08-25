# PyMCU -- outline-on-init: @outline written on a constructor.
#
# Regression for PyMCU#104. The build stopped with "class 'A' cannot be constructed: it has no
# __init__ method (PyMCU does not synthesize a default constructor -- add
# `def __init__(self): ...`)", on a file that defines __init__ on the line under the decorator.
# A constructor establishes the instance and cannot be shared, so the decorator is ignored and
# the class compiles as it does without it.
#
# GPIOR0 reads 0 out of reset, so s is 4 and g() answers 5.
#
# Expected UART output:
#   5
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, outline


class A:
    @outline
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def g(self) -> uint8:
        return self.a + 1


def main() -> None:
    s: uint8 = GPIOR0.value + 4
    o = A(s)
    print(o.g())
    print("done")
