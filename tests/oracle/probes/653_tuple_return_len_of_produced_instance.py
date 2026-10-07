# expect: match
# doc: https://docs.pymcu.org/limitations/
# len(make()): __len__ declares -> uint8, so the produced scalar is never the
# instance -- even though its byte shares the single-field carrier's storage.
# A value-decided refusal that followed the alias to the carrier rejected
# this program.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class C:
    def __init__(self, n: uint8) -> None:
        self.n = n

    def __len__(self) -> int:
        return self.n


def make(n: uint8) -> C:
    return C(n)


def f():
    return len(make(GPIOR0.value + 3)), 0


a, b = f()
print(a, b)
print("END")
