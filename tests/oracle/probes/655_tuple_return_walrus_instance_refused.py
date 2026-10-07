# expect: refuse instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# (x := make()): the walrus's stored temp is the produced instance, so the
# tuple element is the object no matter how the expression is spelled. The
# walrus binding used to drop the class, and bool(a) read a dead slot as
# False where CPython holds the object.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def make(n: uint8) -> Pair:
    return Pair(n, n + 1)


def f():
    return (x := make(GPIOR0.value + 1)), 0


a, b = f()
print(bool(a))
print("END")
