# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `for x in [5]` rebinds x to a byte every iteration it runs: after the loop
# the name holds the last element, not the Pair it was built with. CPython: 5.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2)
    for x in [5]:
        pass
    return x, 0


a, b = f()
print(a + b)
print("END")
