# expect: refuse instance of 'Pair'
# doc: https://docs.pymcu.org/limitations/
# d[0].make() with make() declaring -> Pair, a two-field class: the instance
# leaves through the constructor's target rather than the result temp, so the
# result carries no class tag at all -- the callee's declared return type is
# what names it here. The caller's bool(a) would read a dead slot as False
# where CPython holds the object.
from pymcu.types import uint8


class Pair:
    def __init__(self, a: uint8, b: uint8) -> None:
        self.a = a
        self.b = b


class Factory:
    def __init__(self) -> None:
        self._x = 1

    def make(self) -> Pair:
        return Pair(3, 4)


class D:
    def __init__(self) -> None:
        self.factory = Factory()

    def __getitem__(self, k: uint8) -> Factory:
        return self.factory


d = D()


def f():
    return d[0].make(), 5


a, b = f()
print(bool(a))
print("END")
