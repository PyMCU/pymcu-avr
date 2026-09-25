from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, inline

class Dev:
    @inline
    def __init__(self, big: uint16, small: uint16):
        self.a = bytearray(big)
        self.b = bytearray(small)

    @inline
    def poke(self, i: uint16, v: uint8) -> None:
        self.b[i] = v

    @inline
    def peek(self, i: uint16) -> uint8:
        return self.b[i]

    @inline
    def apeek(self, i: uint16) -> uint8:
        return self.a[i]

big: uint16 = uint16(GPIOR0.value) + 300
small: uint16 = uint16(GPIOR0.value) + 4
d: Dev = Dev(big, small)
d.poke(0, 0x5A)
print(d.peek(0))
print(d.apeek(44))
print("done")
