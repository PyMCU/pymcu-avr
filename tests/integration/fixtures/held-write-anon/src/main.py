# PyMCU -- held-write-named: a write through a method on a field holding an ANONYMOUS instance.
#
# Regression for PyMCU#183. `outer.go()` forwards to `self.inner.set()`. The write-back was
# emitted to the held instance's own flattened field, but nobody ever marked that field as
# needing storage -- the only mention of `inner` in the program is `outer.inner`, so the
# marking walk never saw it -- and here the held instance has no name at all until lowering
# mints one, so the scan pass records the written leaves against the AST node and the lowering
# claims them the moment the name exists. Unmarked, the constructor never materialized its
# store and the
# read folded the constructor's value: 7 instead of 14, compiling clean.
#
# Deliberately minimal. A second construction of the same class invalidates the constant, the
# read stops folding, and the program then answers correctly by accident -- an earlier version
# of this fixture held three chains and passed against the unfixed compiler for exactly that
# reason. One construction, one write, one read.
#
# Seeded from GPIOR0 and never a literal: folding a constructor's 0 and loading a stored 0 are
# the same byte. With GPIOR0 = 7, GPIOR1 must read 14.
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1


class Inner:
    def __init__(self, v: uint8):
        self.v: uint8 = v

    def set(self, n: uint8) -> None:
        self.v = n


class Outer:
    def __init__(self, inner: Inner):
        self.inner = inner

    def go(self, n: uint8) -> None:
        self.inner.set(n)



outer = Outer(Inner(GPIOR0.value))


def touch() -> None:
    outer.go(GPIOR0.value + 7)


def main() -> None:
    touch()
    GPIOR1.value = outer.inner.v
    asm("BREAK")
    while True:
        pass
