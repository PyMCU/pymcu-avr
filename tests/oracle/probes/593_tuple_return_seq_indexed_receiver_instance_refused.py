# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# factories[0].make(): the receiver is an element of a compile-time list of
# instances -- factories__0 holds the Factory -- but the receiver lookup only
# knew the Cls[N] spelling of an index. The method's -> Counter went unseen and
# the instance rode a scalar slot to the caller's bool(a) False, where CPython
# holds the object.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Factory:
    def __init__(self) -> None:
        self._x = 0

    def make(self) -> Counter:
        return Counter(0)


factories = [Factory()]


def f():
    return factories[0].make(), 5


a, b = f()
print(bool(a))
print("END")
