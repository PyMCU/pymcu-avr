# expect: match
# doc: https://docs.pymcu.org/roadmap/
# The walrus rebinds its name too: `(x := 5)` after `x = Pair()` leaves a byte,
# and the old class record must not describe it.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2)
    y = (x := 5)
    return x, y


a, b = f()
print(a + b)
print("END")
