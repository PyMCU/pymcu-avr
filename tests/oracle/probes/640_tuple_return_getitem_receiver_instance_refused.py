# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# d[0].make(): the receiver comes out of __getitem__, a spelling the
# receiver-syntax probes never named. The refusal asks the dispatched callee:
# make() declares -> Counter, so the tuple element is an instance the same way
# a bare Counter(0) is, and the caller's bool(a) would read a dead slot as
# False where CPython holds the object.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Factory:
    def __init__(self) -> None:
        self._x = 0

    def make(self) -> Counter:
        return Counter(0)


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
