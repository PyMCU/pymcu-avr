# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x, y = (p, 6)` binds x to the object p names -- a structural alias, not a
# byte copy -- so x.a keeps reading the field. CPython prints 3 4 6.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


def f():
    p = Pair(3, 4)
    x, y = (p, 6)
    return x.a, x.b, y


a, b, c = f()
print(a * 100 + b * 10 + c)
print("END")
