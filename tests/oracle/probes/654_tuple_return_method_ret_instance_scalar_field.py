# expect: match
# doc: https://docs.pymcu.org/limitations/
# c.dup().n: dup() declares -> C, so the produced value IS an instance -- but
# the .n read on it is the field's scalar, which shares the carrier's byte
# on a single-field class. The scalar rides the tuple slot legitimately.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class C:
    def __init__(self, n: uint8) -> None:
        self.n = n

    def dup(self) -> "C":
        return C(self.n + 1)


def f():
    c = C(GPIOR0.value + 3)
    return c.dup().n, 0


a, b = f()
print(a, b)
print("END")
