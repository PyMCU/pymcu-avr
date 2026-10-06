# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# xs: list[uint8] = [c] stores an element a flattened instance has no byte for:
# the bare handle names storage nothing writes, so xs[0] read False where
# CPython keeps the object and bool() answers True.
from pymcu.types import uint8


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


c = Counter(0)
xs: list[uint8] = [c]
print(bool(xs[0]))
print("END")
