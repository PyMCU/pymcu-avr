# expect: match
# doc: docs/language/roadmap.md:27
# A method called on the instance a factory RETURNED. It used to be refused: the receiver
# was not a name bound to an object. Once the factory result temporary carries its instance
# tag, the method dispatches and reads the instance the call produced, the same fix that
# binds `rd(make(2))` parameters.
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
