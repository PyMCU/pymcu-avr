# expect: refuse instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# flag and make(1): `and` evaluates to the operand, so the result temp is the
# make() instance's carrier whenever flag is truthy. The temp used to carry
# no class and the instance rode a scalar slot to bool(a) False, where
# CPython holds the object.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def make(n: uint8) -> Pair:
    return Pair(n, n + 1)


def f(flag: bool):
    return (flag and make(1)), 7


flag = GPIOR0.value == 0
a, b = f(flag)
print(bool(a))
print("END")
