# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A scalar-producing expression built around a tuple-returning @inline call must
# not read the inner expansion's result slots back as its own value: the result
# list is consumed by the site that asked for it and cleared. print() of such an
# expression used to stream "(e0, e1)" -- the leftover slots of the LAST tuple
# expansion inside the argument -- instead of the scalar the call produced.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


@inline
def pair(v: uint8) -> (uint8, uint8):
    return v, v + 1


def add2(a: uint8, b: uint8) -> uint8:
    return a + b


def add4(a: uint8, b: uint8, c: uint8, d: uint8) -> uint8:
    return a + b + c + d


s = GPIOR0.value
print(add2(pair(s + 1)[0], pair(s + 9)[0]))   # 1 + 9 = 10, not "(9, 10)"
print(abs(pair(s + 3)[0] - 8))                # abs(-5) = 5, not "(3, 4)"
print(min(pair(s + 3)[0], s + 9))             # 3
print(uint8(pair(s + 3)[0]))                  # 3
print(add4(*pair(s + 1), *pair(s + 9)))       # 1+2+9+10 = 22
print(pair(s + 7))                            # the repr itself: (7, 8)
print("END")
