# expect: match
# doc: https://docs.pymcu.org/roadmap/
# Inside a function the same capture stays function-scoped, and the local fold the name
# carried from the declaration above the match does not survive the bind: the arm used
# to return the declared 4 instead of the captured field.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


class P:
    __match_args__ = ("x",)

    def __init__(self, x: uint8) -> None:
        self.x: uint8 = x


px: uint8 = 99


def pick(p: P) -> uint8:
    px: uint8 = 4
    match p:
        case P(x=px):
            return px
    return px


print(pick(P(GPIOR0.value + 1)))
print(pick(P(GPIOR0.value + 2)))
print(px)
print("END")
