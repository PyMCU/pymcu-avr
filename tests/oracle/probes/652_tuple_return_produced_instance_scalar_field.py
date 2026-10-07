# expect: match
# doc: https://docs.pymcu.org/limitations/
# make().n: the field read evaluates to a scalar that SHARES the single-field
# carrier's storage. The byte is the field, not the object, so the tuple slot
# keeps it -- the refusal asks what the value IS, and an alias walk that
# answered the class here read the storage, not the value.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class C:
    def __init__(self, n: uint8) -> None:
        self.n = n


def make(n: uint8) -> C:
    return C(n)


def f():
    return make(GPIOR0.value + 3).n, 0


a, b = f()
print(a, b)
print("END")
