# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x, *y = (5, 6)` rebinds x to a byte: the maybe-instance mark dies with the
# old binding. CPython prints 5.
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
    x, *y = (5, 6)
    return x


print(f())
print("END")
