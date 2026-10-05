# expect: refuse '.extend()' cannot take an instance of 'Counter'
# doc: docs/language/limitations.md:983
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n

    @property
    def n(self) -> uint8:
        return self._n


xs = []
xs.extend([Counter(0), Counter(1)])
a0 = xs[0]
a1 = xs[1]
print(a0.n, a1.n)
print("END")
