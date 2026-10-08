# expect: match
# doc: https://docs.pymcu.org/roadmap/
# The run-time loop's rebind is the same write: `for x in range(3)` leaves x at
# the last value the iterator produced. CPython: 2.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2)
    for x in range(3):
        pass
    return x, 0


a, b = f()
print(a + b)
print("END")
