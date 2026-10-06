# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# factory.make() declares -> Counter, so the tuple element is an instance the
# same way a bare Counter(0) is: it has no scalar slot to cross in, and the
# caller's bool(a) would read a dead slot as False.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Factory:
    def __init__(self) -> None:
        self._x = 0

    def make(self) -> Counter:
        return Counter(0)


factory = Factory()


def f():
    return factory.make(), 5


a, b = f()
print(bool(a))
print("END")
