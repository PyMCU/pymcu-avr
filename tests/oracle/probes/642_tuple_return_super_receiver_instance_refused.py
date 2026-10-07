# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# super().make() inside a method that returns a tuple: the receiver is the
# super-object spelling, but the dispatched callee still declares -> Counter,
# so the element is an instance and the caller's bool(a) would read a dead
# slot as False where CPython holds the object.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Base:
    def __init__(self) -> None:
        self._x = 1

    def make(self) -> Counter:
        return Counter(0)


class Sub(Base):
    def __init__(self) -> None:
        super().__init__()

    def f(self):
        return super().make(), 5


s = Sub()
a, b = s.f()
print(bool(a))
print("END")
