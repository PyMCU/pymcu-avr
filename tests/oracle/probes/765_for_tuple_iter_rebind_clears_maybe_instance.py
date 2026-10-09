# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `for x in (5,)` rebinds x to a byte: after the loop the name holds the last
# element, not whatever it may have been bound to before. CPython prints 5.
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
    for x in (5,):
        pass
    return x


print(f())
print("END")
