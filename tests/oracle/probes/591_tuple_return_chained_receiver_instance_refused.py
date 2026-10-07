# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# holder.factory.make() declares -> Counter just like the bare-name receiver
# in 581: the tuple element is an instance whatever the receiver's syntax, and
# the caller's bool(a) would read a dead slot as False.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Factory:
    def __init__(self) -> None:
        self._x = 0

    def make(self) -> Counter:
        return Counter(0)


class Holder:
    def __init__(self) -> None:
        self.factory = Factory()


holder = Holder()


def f():
    return holder.factory.make(), 5


a, b = f()
print(bool(a))
print("END")
