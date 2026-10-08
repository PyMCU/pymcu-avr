# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# `x` is a byte on one arm and a Pair on the other: the branch merge used to
# intersect the class metadata away, and the scalar slot the caller receives
# carried the byte path's value where CPython may hand it the object. There is
# no representation for "scalar or instance depending on the path", so the
# tuple element is refused by name.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    if GPIOR0.value != 0:
        x = 7
    else:
        x = Pair(1, 2)
    return x, 0


a, b = f()
print(a + b)
print("END")
