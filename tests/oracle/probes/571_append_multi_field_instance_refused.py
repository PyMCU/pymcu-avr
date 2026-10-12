# expect: refuse 'n' is called on an instance read out of a growable list
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
# A class of several scalar fields appended to a list is a heap record (RFC 0016); its fields read
# back, but a property or method runs on it only if it is @outline, so this getter is refused.
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
