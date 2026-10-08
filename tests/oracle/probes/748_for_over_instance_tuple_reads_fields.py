# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `for x in (p,)` rebinds a scalar name to the OBJECT: the stale constant the
# earlier `x = 5` left must not fold `x.a` while x IS p. CPython: 3.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = 5
    p = Pair(1, 2)
    y = 0
    for x in (p,):
        y = x.a + x.b
    return y, 0


a, b = f()
print(a + b)
print("END")
