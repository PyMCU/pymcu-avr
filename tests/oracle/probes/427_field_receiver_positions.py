# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# `h.inner.get()`, a method on a class-typed FIELD, which roadmap.md:117 names as the shape
# the compat layers are built on. Four positions, each with the bound-receiver control and
# two holders of different values.
#
# The fifth position, the same read from inside a plain function, is NOT here: it returns 0
# and is probe 430, filed as #520. Keeping them apart means this probe stays a green that
# asserts something rather than an xfail that hides four working positions.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

tbl = [10, 11, 12, 13]


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


class Holder:
    def __init__(self, n: uint8):
        self.inner: Src = Src(n + GPIOR0.value)


def take(v: uint8):
    return 100 + v


h2 = Holder(2)
h3 = Holder(3)
bound = h2.inner
print(bound.get())
print(h2.inner.get())
print(h3.inner.get())
if h2.inner.get() > 2:
    print(1)
else:
    print(0)
print(take(h2.inner.get()))
print(tbl[h3.inner.get()])
print("END")
