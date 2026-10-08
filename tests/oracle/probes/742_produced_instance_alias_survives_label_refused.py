# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# `x = Pair() if flag else Pair()` binds x to the ternary's result temporary,
# which only CARRIES the class it was produced from. The alias used to be filed
# as value-tracking state, so the label `y = gate and 1` emits dropped it and x
# answered as the scalar slot the constructions never wrote -- printing a stale
# byte where CPython prints the object.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2) if GPIOR0.value != 0 else Pair(3, 4)
    y = (GPIOR0.value != 0) and 1
    return x, 0


a, b = f()
print(a + b)
print("END")
