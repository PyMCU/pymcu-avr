# for-over-instance-tuple: adafruit_character_lcd's
#   for pin in (reset_dio, enable_dio, d4, d5, d6, d7)
# unrolls over the named instances. The loop variable carries each pin's
# compile-time identity (the same way pin.direction = OUTPUT talks to the
# right GPIO); it is not a heap alias.
#
# WHAT DISCRIMINATES:
#   3  -- first instance's n
#   5  -- second instance's n
from pymcu.time import delay_ms


class Pin:
    def __init__(self, n: int):
        self.n = n

    def get(self) -> int:
        return self.n


class Lcd:
    def __init__(self, a: Pin, b: Pin):
        for p in (a, b):
            print(p.get())


def main():
    while True:
        Lcd(Pin(3), Pin(5))
        print("END")
        delay_ms(1200)
