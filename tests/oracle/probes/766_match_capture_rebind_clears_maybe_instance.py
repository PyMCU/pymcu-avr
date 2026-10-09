# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `case _ as x:` binds x to the subject: the maybe-instance mark the branch
# merge left must die with the old binding. CPython prints 9.
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
    match 9:
        case _ as x:
            return x
    return 0


print(f())
print("END")
