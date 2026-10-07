# expect: refuse instance of 'C'
# doc: https://docs.pymcu.org/limitations/
# c or 7 with a truthy c: `or` evaluates to the LEFT operand, so the result
# is the instance itself. The truthiness byte the lowering used to copy read
# 1, where CPython's a IS the object -- the operand's class rides the result
# the way a call result's does.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class C:
    def __init__(self, n: uint8) -> None:
        self.n = n

    def __bool__(self) -> bool:
        return self.n != 0


c = C(GPIOR0.value + 1)


def f():
    return (c or 7), 0


a, b = f()
print(bool(a))
print("END")
