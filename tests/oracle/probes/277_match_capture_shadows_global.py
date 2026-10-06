# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A `match` class-pattern capture is a BINDING: at module level it binds the module
# global of that name, exactly as CPython does, so every read inside the arm sees the
# captured field and not the global's old value. The capture used to be filed under
# `main.px` while every read answered the bare `px`, so the arm bound nothing.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


class P:
    __match_args__ = ("x",)

    def __init__(self, x: uint8) -> None:
        self.x: uint8 = x


px: uint8 = 99
a = P(GPIOR0.value + 1)
match a:
    case P(x=px):
        print(px)
print(px)
print("END")
