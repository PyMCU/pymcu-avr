# expect: match
# doc: https://docs.pymcu.org/limitations/
# Same __getitem__ receiver as 640, but the dispatched callee declares ->
# uint8: a scalar answer rides the tuple slot legitimately, so this program
# must keep compiling -- the refusal keys on the callee's return class, not
# on the receiver's shape.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Factory:
    def __init__(self) -> None:
        self._x = 7 + GPIOR0.value

    def val(self) -> uint8:
        return self._x


class D:
    def __init__(self) -> None:
        self.factory = Factory()

    def __getitem__(self, k: uint8) -> Factory:
        return self.factory


d = D()


def f():
    return d[0].val(), 5


a, b = f()
print(a + b)
print("END")
