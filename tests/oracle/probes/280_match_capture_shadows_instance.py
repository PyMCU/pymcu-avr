# expect: match
# doc: docs/language/roadmap.md
# A sub-pattern capture holds the FIELD's value, a scalar. Inheriting the class of the
# global it shadows refused `f"{px}"` inside the arm as "an instance of 'P'", a
# rejection of a program CPython runs. The `as` capture, which DOES hold the subject,
# keeps its class -- and, when it names the subject itself, files no self-alias.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print


class P:
    __match_args__ = ("x",)

    def __init__(self, x: uint8) -> None:
        self.x: uint8 = x


px = P(3)
a = P(GPIOR0.value + 1)
match a:
    case P(x=px):
        print(f"{px}")

b = P(GPIOR0.value + 5)
match b:
    case P() as b:
        print(b.x)
print(b.x)
print("END")
