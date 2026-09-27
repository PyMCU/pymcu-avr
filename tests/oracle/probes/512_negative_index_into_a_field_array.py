# expect: match
# doc: docs/language/limitations.md:53
# `self.buf[-1]` is the last element. The named-array read normalized a negative constant
# index, but the stores and every access through a field handed it to the backend as it was:
# the field read printed the bytes in front of the array and the stores did not assemble.
from pymcu.types import uint8, uint16, inline
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value


class R:
    def __init__(self):
        self.b: uint16[4] = [0] * 4

    def last(self) -> uint16:
        return self.b[-1]

    @inline
    def first(self) -> uint16:
        return self.b[-4]

    def put(self, x: uint16):
        self.b[-1] = x
        self.b[-2] += x


r = R()
r.b[0] = s + 7
r.put(s + 1004)
r.b[-3] = s + 60000
x: uint8 = s + 9
print(x, r.last(), r.first(), r.b[s + 2], r.b[s + 1])

g: uint16[4] = [0] * 4
g[-1] = s + 1000
g[-2] += s + 2000
print(g[3], g[2], g[-1])
print("END")
