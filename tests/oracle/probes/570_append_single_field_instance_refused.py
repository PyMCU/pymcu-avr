# expect: refuse '.append()' cannot take an instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n

    @property
    def n(self) -> uint8:
        return self._n


xs = []
xs.append(Counter(0))
xs.append(Counter(1))
a0 = xs[0]
a1 = xs[1]
print(a0.n, a1.n)
print("END")
