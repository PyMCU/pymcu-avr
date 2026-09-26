# expect: match
# doc: docs/language/limitations.md:53
# Five constructions used as the INDEX of a subscript. That column of the corpus was empty:
# nothing but a bare name, a literal or plain arithmetic had ever appeared inside `[...]`,
# so `a[o.m()]`, `a[len(b)]`, `a[b[i]]` and `a[x if c else y]` were all unexercised.
#
# Each construction is printed on its own line first and then used as the index, so the pair
# is inside one program: the control says what the subexpression is worth, and the indexed
# read says what the index position made of it. tbl[v] is 10 + v, so a wrong index is a
# wrong number rather than a silent same-answer. `o.get()` is annotated -> uint8 on purpose,
# to keep #478 and #519 out of this cell.
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
print(o.get())
print(tbl[o.get()])
print(len(buf))
print(tbl[len(buf)])
print(src[idx])
print(tbl[src[idx]])
print(0 if flag else 9)
print(tbl[0 if flag else 9])
print(o.base + 1)
print(tbl[o.base + 1])
print("END")
