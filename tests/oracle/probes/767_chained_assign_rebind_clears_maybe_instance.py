# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `y = x = 5` binds x to a byte: the maybe-instance mark dies with the old
# binding. CPython prints 55.
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
    y = x = 5
    return x * 10 + y


print(f())
print("END")
