# expect: match
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
# A list comprehension inside an `@inline` method, read at two different indices.
#
# Both halves are the SAME body, once in an `@inline` method and once in a plain one, so a
# divergence between them is the finding even if both compile. Each half is expanded TWICE
# with different values: `@inline` promises one slot per expansion, and a shared slot only
# shows when the two expansions must print different numbers. That property is what made
# #519 visible in this same sweep; with one expansion every cell in this column passed.
# The instances are seeded from GPIOR0 so nothing folds at compile time.
from pymcu.types import inline, uint8
from pymcu.chips.atmega328p import GPIOR0


class Inl:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    @inline
    def probe(self, k: uint8):
        vals = [x * self.base for x in range(3)]
        return vals[k]


class Pln:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def probe(self, k: uint8):
        vals = [x * self.base for x in range(3)]
        return vals[k]


ia = Inl(3 + GPIOR0.value)
ib = Inl(10 + GPIOR0.value)
pa = Pln(3 + GPIOR0.value)
pb = Pln(10 + GPIOR0.value)
print(ia.probe(1))
print(ib.probe(2))
print(pa.probe(1))
print(pb.probe(2))
print("END")
