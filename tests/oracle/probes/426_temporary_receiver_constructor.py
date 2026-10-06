# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A method called on a receiver that is NOT a name bound to an object. The corpus had this
# shape in twelve probes, all at module level or inside a method, and never in a condition,
# as an argument or as an index.
#
# Each position carries the bound-receiver control beside it and TWO temporaries of
# different values, so a slot shared between the two expansions shows as two equal numbers
# instead of a silent pass. That property is what found #519 in this sweep.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

tbl = [10, 11, 12, 13]


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


def take(v: uint8):
    return 100 + v


def via_return():
    return Src(3 + GPIOR0.value).get()


bound = Src(2 + GPIOR0.value)
print(bound.get())
print(Src(2 + GPIOR0.value).get())
print(Src(3 + GPIOR0.value).get())
if Src(2 + GPIOR0.value).get() > 2:
    print(1)
else:
    print(0)
if Src(3 + GPIOR0.value).get() > 2:
    print(1)
else:
    print(0)
print(take(Src(2 + GPIOR0.value).get()))
print(tbl[Src(3 + GPIOR0.value).get()])
print(via_return())
print("END")
