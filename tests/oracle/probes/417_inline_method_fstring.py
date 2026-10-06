# expect: match
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
# An f-string inside an `@inline` method. `print` dispatches on the syntactic shape of its
# argument through a long ladder, so a new position for a formatted value is a new branch of
# it; the corpus had f-strings at module and function level only, never in a method.
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
        print(f"v{self.base}k{k}")


class Pln:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def probe(self, k: uint8):
        print(f"v{self.base}k{k}")


ia = Inl(3 + GPIOR0.value)
ib = Inl(10 + GPIOR0.value)
pa = Pln(3 + GPIOR0.value)
pb = Pln(10 + GPIOR0.value)
ia.probe(1)
ib.probe(2)
pa.probe(1)
pb.probe(2)
print("END")
