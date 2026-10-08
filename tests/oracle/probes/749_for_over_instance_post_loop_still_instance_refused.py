# expect: refuse cannot be an instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# CPython leaves the loop variable bound to the last element: after
# `for x in (p,)` the name still IS the object, so the scalar tuple slot cannot
# carry it even though `x` was a plain byte before the loop.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x = 5
    p = Pair(1, 2)
    for x in (p,):
        pass
    return x, 0


a, b = f()
print(a + b)
print("END")
