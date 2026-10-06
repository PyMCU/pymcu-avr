# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# bins[0][0] takes the nested-list fast path: a class instance has no scalar
# element byte to store there either (fields live at their own slots), so the
# store is refused instead of landing a stale byte that bool() reads as False.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self.n = n


bins: list[list[uint8]] = [[0]]
bins[0][0] = Counter(0)
print(bool(bins[0][0]))
