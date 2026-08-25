# PyMCU -- super-outlined: super().method() from an outlined override.
#
# Regression for PyMCU#97. Both methods undecorated is the DEFAULT path (outlined), and
# there the base body read every inherited field as zero: the base method ran, it just
# never received the instance state, so the override computed from 0 with no diagnostic.
#
# Inside an outlined method there is no `self` to alias: the instance arrives as one
# parameter per field. The base body reads those names literally, so the values are copied
# across before its body is expanded.
#
# Seeded from GPIOR0 so nothing folds. With GPIOR0 = 5:
#   GPIOR1 (0x4A) = Sub(5).g()  = (5 + 1) * 2 = 12
#   GPIOR2 (0x4B) = Sub(5).h()  = 5 + 40      = 45   (two inherited fields)
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, asm


class Base:
    def __init__(self, a: uint8, b: uint8):
        self.a: uint8 = a
        self.b: uint8 = b

    def g(self) -> uint8:
        return self.a + 1

    def h(self) -> uint8:
        return self.a + self.b


class Sub(Base):
    def g(self) -> uint8:
        return super().g() * 2

    def h(self) -> uint8:
        return super().h()


def main():
    seed: uint8 = GPIOR0.value
    s = Sub(seed, 40)
    GPIOR1.value = s.g()
    GPIOR2.value = s.h()
    asm("BREAK")
    while True:
        pass
