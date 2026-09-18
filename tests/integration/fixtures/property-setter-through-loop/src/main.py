# property-setter-through-loop: adafruit_character_lcd's
#   for pin in (reset_dio, enable_dio, ...):
#       pin.direction = Direction.OUTPUT
# The loop variable is each named instance; direction is a @property setter,
# not a method assignment.
#
# WHAT DISCRIMINATES:
#   1  -- first instance after the setter
#   1  -- second instance after the setter
from pymcu.time import delay_ms


class Pin:
    def __init__(self):
        self._d = 0

    @property
    def direction(self):
        return self._d

    @direction.setter
    def direction(self, d: int):
        self._d = d

    def get(self) -> int:
        return self._d


class Lcd:
    def __init__(self, a: Pin, b: Pin):
        for p in (a, b):
            p.direction = 1
            print(p.get())


def main():
    while True:
        Lcd(Pin(), Pin())
        print("END")
        delay_ms(1200)
