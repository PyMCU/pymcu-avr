# expect: match
# doc: https://docs.pymcu.org/roadmap/
# Iterating a variable-backed array rebinds x per element: inside the body x IS
# the element, and a stale class left from `x = Pair()` used to refuse or
# answer the read as the dead object. CPython: 6.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = Pair(1, 2)
    xs = [5, 6]
    y = 0
    for x in xs:
        y = x
    return y, 0


a, b = f()
print(a + b)
print("END")
