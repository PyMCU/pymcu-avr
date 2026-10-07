# expect: refuse instance of 'Factory'
# doc: https://docs.pymcu.org/limitations/
# return d[0], 5: the element is not a call at all, yet the __getitem__
# dispatch produces a Factory instance, so it still has no scalar slot to
# ride. The caller's bool(a) would read a dead slot as False where CPython
# holds the object.
from pymcu.types import uint8


class Factory:
    def __init__(self) -> None:
        self._x = 0


class D:
    def __init__(self) -> None:
        self.factory = Factory()

    def __getitem__(self, k: uint8) -> Factory:
        return self.factory


d = D()


def f():
    return d[0], 5


a, b = f()
print(bool(a))
print("END")
