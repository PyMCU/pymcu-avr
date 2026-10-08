# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# The same union feeding a bare `return x`: the tuple check's twin. The result
# slot copy would hand the caller the scalar path's byte where CPython may pass
# the object.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    if GPIOR0.value != 0:
        x = 7
    else:
        x = Pair(1, 2)
    return x


a = f()
print(a)
print("END")
