# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `with M() as x` rebinds x to the entered value: the maybe-instance mark the
# branch merge left must die with the old binding. CPython prints 5.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


class M:
    def __enter__(self):
        return 5

    def __exit__(self, a: uint8, b: uint8, c: uint8) -> None:
        pass


def f():
    if GPIOR0.value != 0:
        x = Pair(1, 2)
    else:
        x = 1
    with M() as x:
        return x


print(f())
print("END")
