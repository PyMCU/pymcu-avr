# expect: refuse '.append()' cannot take an instance of 'Pair'
# doc: docs/language/limitations.md:983
from pymcu.types import uint8


class Pair:
    def __init__(self, n: uint8, m: uint8) -> None:
        self._n = n
        self._m = m

    @property
    def n(self) -> uint8:
        return self._n


xs = []
xs.append(Pair(0, 10))
xs.append(Pair(1, 11))
a0 = xs[0]
a1 = xs[1]
print(a0.n, a1.n)
print("END")
