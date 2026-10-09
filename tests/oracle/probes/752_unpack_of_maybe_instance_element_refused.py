# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# `y, z = x, 1` unpacks a name that may still be a Pair into a scalar slot:
# there is no representation for the object on the byte path, so the element
# is refused by name instead of copying the dead slot.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    if GPIOR0.value != 0:
        x = Pair(1, 2)
    else:
        x = 1
    y, z = x, 1
    return y, z


a, b = f()
print(a + b)
print("END")
