# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# Indexes the compiler can see are in range: a mask, a range() counter inside the length, a byte
# into 256 elements. They keep working (and are not checked).
from pymcu.types import uint8

xs: uint8[8] = [0, 1, 2, 3, 4, 5, 6, 7]
big: uint8[256] = [0] * 256


def masked(i: int) -> int:
    return xs[i & 7]


def total() -> int:
    s = 0
    for i in range(8):
        s = s + xs[i]
    return s


def wide(b: uint8) -> int:
    return big[b]


print(masked(11))
print(total())
print(wide(200))
print("END")
