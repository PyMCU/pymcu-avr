# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# relay() declares no return type; its body returns what make() dispatched
# to. The class the resolved callee produced rides the temp the caller
# receives, so an unannotated hop does not launder the instance into a
# scalar slot the way it did when only the element's spelling was asked.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


def make(n: uint8) -> Counter:
    return Counter(n)


def relay():
    return make(3)


def f():
    return relay(), 0


a, b = f()
print(bool(a))
print("END")
