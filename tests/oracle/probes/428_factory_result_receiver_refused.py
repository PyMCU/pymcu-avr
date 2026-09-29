# expect: match
# doc: docs/language/roadmap.md:27
# A method called on the instance a factory RETURNED. The diagnostic names the rule rather
# than the symptom: a receiver has to be a name bound to an object, a register, or a value
# PyMCU defines methods on. Measured identical in all five positions (value, condition,
# argument, index, return), so one probe pins the rule and the positions do not each need
# their own.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base

    def get(self) -> uint8:
        return self.base


def make(n: uint8) -> Src:
    return Src(n + GPIOR0.value)


print(make(2).get())
print("END")
