# expect: match
# doc: https://docs.pymcu.org/roadmap/
# The capture's qualifier was the only one of the seven that never consulted the inline
# prefix, so two expansions of one @inline shared a single capture slot and both read
# the module global instead of their own field.
from pymcu.types import uint8, inline
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


class P:
    __match_args__ = ("x",)

    def __init__(self, x: uint8) -> None:
        self.x: uint8 = x


@inline
def show(p: P) -> None:
    match p:
        case P(x=px):
            print(px)


px: uint8 = 99
show(P(GPIOR0.value + 1))
show(P(GPIOR0.value + 2))
print(px)
print("END")
