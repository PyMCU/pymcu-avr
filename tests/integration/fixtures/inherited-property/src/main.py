# PyMCU -- inherited-property: property setters must resolve through the MRO
# and through the aliases a compile-time-unrolled loop binds.
#
# adafruit_character_lcd's constructor does `for pin in (a, b, ...):
# pin.direction = OUTPUT`, and its `message` setter lives on the base class
# while the instance is a subclass. Three things had to hold at once:
#   1. a tuple of INSTANCES unrolls -- the loop variable aliases the object,
#   2. `pin.direction = v` expands the property setter on that alias,
#   3. `sub.message = "..."` finds the setter on the BASE class and binds a
#      `str` parameter whose text is known, so `for c in s` unrolls inside.
#
# Expected UART output:
#   A 1 / B 1 / C 3 / D 1
#   END
from pymcu.hal.console import print
from pymcu.types import uint8


class Pin:
    def __init__(self, n: uint8):
        self._n = n
        self._dir = 0

    @property
    def direction(self) -> uint8:
        return self._dir

    @direction.setter
    def direction(self, d: uint8):
        self._dir = d


class Base:
    def __init__(self, a, b):
        for pin in (a, b):
            pin.direction = 1
        self._count = 0

    @property
    def message(self) -> str:
        return ""

    @message.setter
    def message(self, m: str):
        n: uint8 = 0
        for c in m:
            n = n + 1
        self._count = n


class Sub(Base):
    def __init__(self, a, b):
        super().__init__(a, b)


def main():
    pa = Pin(3)
    pb = Pin(4)
    lcd = Sub(pa, pb)
    print("A", pa.direction)
    print("B", pb.direction)
    lcd.message = "abc"
    print("C", lcd._count)
    lcd.message = "x"
    print("D", lcd._count)
    print("END")


main()
