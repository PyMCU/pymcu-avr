# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x = 5` rebinds the name to a byte: the instance mark the construction left
# must die with the old binding, or the scalar reads as the dead object (and a
# tuple slot refuses a name that is no longer an instance). CPython: 5.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2)
    x = 5
    return x, 0


a, b = f()
print(a + b)
print("END")
