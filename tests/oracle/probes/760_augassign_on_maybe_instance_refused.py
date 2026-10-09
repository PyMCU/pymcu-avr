# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# `x += 1` is a read-modify-write on the name's slot: on a path where x is a
# flattened Pair there is no scalar slot to update, so the read would feed
# stale bytes into the write. Refused by name.
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
    x += 1
    return x


print(f())
print("END")
