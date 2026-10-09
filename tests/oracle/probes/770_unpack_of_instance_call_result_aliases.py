# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x, y = (Pair(3, 4), 6)`: the instance element binds structurally rather
# than copying dead bytes. CPython prints 346.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    x, y = (Pair(3, 4), 6)
    return x.a, x.b, y


a, b, c = f()
print(a * 100 + b * 10 + c)
print("END")
