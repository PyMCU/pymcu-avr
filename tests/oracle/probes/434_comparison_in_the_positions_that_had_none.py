# expect: match
# doc: https://docs.pymcu.org/language-reference/#primitive-types
# A comparison as a call ARGUMENT, as an instance FIELD and as a subscript INDEX. The corpus
# had 48 comparisons in a condition, eleven inside print() and exactly one as a value, and
# none at all in these three.
#
# Each position carries both a true and a false comparison, because a position that always
# yields the same answer cannot tell a working comparison from a folded one. `base > k` is
# false and `base > 1` is true, so `tbl[...]` reads two different cells and `take(...)`
# returns two different numbers.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

base = 3 + GPIOR0.value
k = 5 + GPIOR0.value
tbl = [10, 11, 12, 13]


def take(v: uint8):
    return 100 + v


class C:
    def __init__(self):
        self.a: uint8 = base > k
        self.b: uint8 = base > 1


print(take(base > k))
print(take(base > 1))
c = C()
print(c.a)
print(c.b)
print(tbl[base > k])
print(tbl[base > 1])
print("END")
