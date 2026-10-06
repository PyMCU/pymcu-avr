# expect: match
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
# A comprehension inside an @inline method, expanded THREE times with different values.
#
# This is 431's row measured again under the property that found #519: with one expansion
# every cell of the @inline column passed, and with two the fourteenth turned out to read an
# 8-bit return as 16 bits from the second expansion on. A comprehension builds a fixed array
# per expansion, so a slot shared between expansions is exactly the failure to look for, and
# the three calls must print three different pairs for the probe to be able to see it.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


class Dev:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    @inline
    def build(self, k: uint8):
        vals = [x * self.base + k for x in range(3)]
        print(vals[0])
        print(vals[2])


a = Dev(3 + GPIOR0.value)
b = Dev(10 + GPIOR0.value)
c = Dev(4 + GPIOR0.value)
a.build(1)
b.build(2)
c.build(3)
print("END")
