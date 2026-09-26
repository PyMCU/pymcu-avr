# expect: match
# doc: docs/language/roadmap.md:27
# A method called on an element of a list of instances, indexed at RUN TIME. It works, at a
# constant index and at a runtime one, and the two elements must answer differently for the
# probe to be able to tell a working index from a folded one.
#
# This probe exists because the sweep that produced it first recorded this row as REFUSED,
# and that was wrong: the refusal came from the sweep's own control line, `bound = lst[i0]`,
# which binds an element to a name. THAT is the refused form, and 438 pins it. The construct
# under test was never the one being refused.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


lst = [Src(2 + GPIOR0.value), Src(3 + GPIOR0.value)]
i0 = GPIOR0.value
print(lst[i0].get())
print(lst[i0 + 1].get())
print(lst[0].get())
print(lst[1].get())
print("END")
