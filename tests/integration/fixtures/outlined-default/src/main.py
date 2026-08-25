# PyMCU -- outlined-default: an omitted argument on an outlined method.
#
# Regression for PyMCU#96. An outlined method is a real subroutine with a fixed parameter
# list, and a call that stopped early left the rest unwritten: `def g(self, k: uint8 = 4)`
# called as `o.g()` computed with k = 0, on a clean build. The defaults were recorded for
# top-level functions only, and the outlined method rewrites its parameter list anyway.
#
# Seeded from GPIOR0 so nothing folds. With GPIOR0 = 8:
#   GPIOR1 (0x4A) = o.g()    = 8 + 4  = 12   (default used)
#   GPIOR2 (0x4B) = o.g(30)  = 8 + 30 = 38   (explicit argument still wins)
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, asm


class A:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def g(self, k: uint8 = 4) -> uint8:
        return self.a + k


def main():
    o = A(GPIOR0.value)
    GPIOR1.value = o.g()
    GPIOR2.value = o.g(30)
    asm("BREAK")
    while True:
        pass
