# expect: match
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
# An @inline overload call `w(buf)` inside an expansion where `buf` is a scalar
# parameter read the bare name as the module-level ARRAY `buf`: dispatch first
# picked the bytearray body, then refused the scalar overload as "a buffer for a
# number parameter". The bare-name array lookup is now gated on the name being
# unbound in the expansion's own scope.
from pymcu.types import uint8, inline
from pymcu.chips.atmega328p import GPIOR0

class Inner:
    @inline
    def w(self, data: bytearray):
        print(2)

    @inline
    def w(self, data: uint8):
        print(1)

class Outer:
    @inline
    def __init__(self):
        self.inner = Inner()

    @inline
    def forward(self, buf: uint8):
        self.inner.w(buf)

buf: uint8[3] = [7, 8, 9]
i: uint8 = GPIOR0.value
o = Outer()
o.forward(buf[i])
print("END")
