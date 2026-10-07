# expect: refuse instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# flag or make(1): the `or` twin of 646 -- with flag falsy the result is the
# make() instance itself, carried by a temp that used to lose the class. The
# instance rode a scalar slot and bool(a) read a dead byte as False where
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
    return (flag or make(1)), 7


flag = GPIOR0.value != 0
a, b = f(flag)
print(bool(a))
print("END")
