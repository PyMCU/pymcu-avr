# PyMCU -- outline-mutating-getter: an @outline method that writes a field AND returns a value.
#
# Regression for PyMCU#95. The returned number was right and the instance kept the old one:
# a Model A method takes its field by value and its single return slot already carries the
# returned expression, so the write had no way back and was silently dropped. The class now
# gets a slot and the body writes through the pointer.
#
# GPIOR0 is the seed: reading it keeps the compiler from folding the whole program to
# constants, which is what a literal would do. It reads 0 out of reset, so s is 5.
#
# Expected UART output:
#   6
#   6
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, outline


class A:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    @outline
    def bump(self) -> uint8:
        self.a = self.a + 1
        return self.a


def main() -> None:
    s: uint8 = GPIOR0.value + 5
    o = A(s)
    print(o.bump())
    print(o.a)
    print("done")
