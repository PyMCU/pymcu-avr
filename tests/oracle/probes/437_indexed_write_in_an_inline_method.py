# expect: match
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
# An indexed WRITE inside an @inline method, expanded twice at two different indices, so each
# expansion needs its own buffer and its own index. The corpus had 17 indexed writes at
# module level, 13 in a function, four in a method and none inside an expansion.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


class Dev:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    @inline
    def store(self, k: uint8):
        b = bytearray(4)
        b[k] = self.base
        b[k + 1] = self.base + 1
        print(b[k])
        print(b[k + 1])


a = Dev(3 + GPIOR0.value)
z = Dev(10 + GPIOR0.value)
a.store(0)
z.store(2)
print("END")
