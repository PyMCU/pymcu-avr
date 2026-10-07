# expect: refuse instance of 'Counter'
# doc: https://docs.pymcu.org/limitations/
# An @outline method dispatched to a real subroutine, wrapped in a ternary:
# neither the call's spelling nor a branch-local registration reaches the
# element check -- the produced temp is what carries the -> Counter answer.
from pymcu.types import outline, uint8
from pymcu.chips.atmega328p import GPIOR0


class Counter:
    def __init__(self, n: uint8) -> None:
        self._n = n


class Fac:
    def __init__(self) -> None:
        pass

    @outline
    def make(self, n: uint8) -> Counter:
        return Counter(n)


fac = Fac()


def f(flag: bool):
    return (fac.make(1) if flag else fac.make(2)), 7


flag = GPIOR0.value == 0
a, b = f(flag)
print(bool(a))
print("END")
