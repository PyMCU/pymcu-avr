# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x, y = (5, 6)` rebinds x wholesale: the maybe-instance mark a branch merge
# left must die with the old binding, or the unpack refuses a name that is no
# longer an instance. CPython prints 5 6.
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
    x, y = (5, 6)
    return x, y


a, b = f()
print(a * 10 + b)
print("END")
