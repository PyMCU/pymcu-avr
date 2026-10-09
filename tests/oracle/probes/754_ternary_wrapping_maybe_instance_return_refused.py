# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# `(x if c else 0)` hides the maybe-instance name under a ternary: the check
# used to look at the syntactic variable only, so the expression slipped past
# and the caller's scalar slot copied stale storage. Refused by name.
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
    return (x if GPIOR0.value != 1 else 0), 1


a, b = f()
print(a + b)
print("END")
