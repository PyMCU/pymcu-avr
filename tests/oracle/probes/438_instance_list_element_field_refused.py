# expect: refuse not addressable at run time
# doc: https://docs.pymcu.org/roadmap/#language
# The counterpart to 429: the same list of instances at the same runtime index, reading a
# FIELD instead of calling a method. The method call compiles and is correct; the field read
# is refused, because the list lives as separate variables and only a constant index can
# reach one. Two spellings of what looks like the same access, and only one of them has
# storage to talk about.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base


lst = [Src(2 + GPIOR0.value), Src(3 + GPIOR0.value)]
i0 = GPIOR0.value
print(lst[i0].base)
print("END")
