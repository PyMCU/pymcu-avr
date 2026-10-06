# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# Every one of the corpus's thirteen `len()` probes called it inside print(). Bound to a
# name, in a condition, as an argument, as an index, as a return value and interpolated were
# all empty cells, which matters because a diagnostic elsewhere in the compiler is known to
# see a literal length and not a len() call.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


o = Src(2 + GPIOR0.value)
flag = GPIOR0.value == 0
src = bytearray(4)
src[0] = 0
src[1] = 1
src[2] = 2
src[3] = 3
idx = 2 + GPIOR0.value
buf = bytearray(3)
tbl = [10, 11, 12, 13]


def take(v: uint8):
    return v * 10


def give():
    return len(buf)


n = len(buf)
print(n)
print(n * 10)
if len(buf) > 2:
    print(1)
else:
    print(0)
print(take(len(buf)))
print(tbl[len(buf)])
print(give())
print(f"{len(buf)}")
print("END")
