# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `except E as x` rebinds x to the raised object: the maybe-instance mark the
# branch merge left must die with the old binding. CPython prints 7.
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
    try:
        raise ValueError(7)
    except ValueError as x:
        return x.args[0] if GPIOR0.value > 250 else 7


print(f())
print("END")
