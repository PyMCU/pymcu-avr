# PyMCU -- list-param-values: a driver takes a table of numbers and a buffer (PyMCU#314,
# PyMCU#315)
#
# Before these, `self._levels = levels` made the field a scalar and every subscript read
# the zero nothing had written -- the firmware built clean and printed 0. The buffer lost
# its storage the same way and `self._data[i]` was refused as a bit index.
#
# `base` comes from GPIOR0 (the test seeds 5) so the buffer is filled on the chip.
#
# Expected UART, seed 5:
#   A 10 / B 30 / C 60 / D 3 / E 7 / F 24 / G 5 / G 6 / G 7 / G 8
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class Table:
    def __init__(self, levels):
        self._levels = levels

    def at0(self) -> uint8:
        return self._levels[0]

    def at2(self) -> uint8:
        return self._levels[2]

    def total(self) -> uint8:
        t: uint8 = 0
        for v in self._levels:
            t = t + v
        return t

    def count(self) -> uint8:
        return len(self._levels)


class Frame:
    def __init__(self, data):
        self._data = data

    def fill(self, base: uint8):
        i: uint8 = 0
        while i < 4:
            self._data[i] = base + i
            i = i + 1

    def get(self, i: uint8) -> uint8:
        return self._data[i]


levels = [7, 8, 9]
buf = bytearray(4)


def main():
    base: uint8 = GPIOR0.value

    t = Table([10, 20, 30])
    print("A", t.at0())
    print("B", t.at2())
    print("C", t.total())
    print("D", t.count())

    u = Table(levels)
    print("E", u.at0())
    print("F", u.total())

    f = Frame(buf)
    f.fill(base)
    i: uint8 = 0
    while i < 4:
        print("G", f.get(i))
        i = i + 1

    print("END")


main()
