# expect: refuse instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# (make(1) if flag else make(2)): the tuple element is a TernaryExpr, not a
# call -- the merge temp used to lose the class each arm's dispatched callee
# produced, so the instance rode a scalar slot and bool(a) read a dead byte
# as False where CPython holds the object.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def make(n: uint8) -> Pair:
    return Pair(n, n + 1)


def f(flag: bool):
    return (make(1) if flag else make(2)), 7


flag = GPIOR0.value == 0
a, b = f(flag)
print(bool(a))
print("END")
