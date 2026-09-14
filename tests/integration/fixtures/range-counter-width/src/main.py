# PyMCU -- range-counter-width: the range() counter is sized from its bounds (PyMCU#284)
#
# The counter of `for i in range(...)` was always an 8-bit variable, whatever the bounds
# said: range(300) ran 44 times (the stop truncated to 44), range(0, 256) never ran, a
# descending range from 200 to -1 never ran (200 as int8 is -56), a uint16 stop variable
# and a uint16 annotation on the loop variable were both ignored, and a step that does not
# divide the span wrapped the counter around (range(0, 250, 30) ran 17 times).
#
# Each line below is one shape, checked against the count CPython gives for the same loop.
# `hundred()` is the guard: range(100) must stay the 8-bit loop it always was.
#
# Expected UART (115200):
#   A 300
#   B 256
#   C 655
#   D 201
#   E 129
#   F 300
#   G 300
#   H 10
#   I 9
#   J 9
#   K 9 1000 143
#   L 100
#   END
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, int8


def trips(stop: uint16, step: uint8) -> uint16:
    c: uint16 = 0
    for i in range(0, stop, step):
        c = c + 1
    return c


def hundred() -> uint16:
    c: uint16 = 0
    for i in range(100):
        c = c + 1
    return c


def main():
    c: uint16 = 0
    for i in range(300):
        c = c + 1
    print("A", c)

    c = 0
    for i in range(0, 256):
        c = c + 1
    print("B", c)

    c = 0
    for i in range(0, 65500, 100):
        c = c + 1
    print("C", c)

    c = 0
    for i in range(200, -1, -1):
        c = c + 1
    print("D", c)

    c = 0
    for i in range(128, -1, -1):
        c = c + 1
    print("E", c)

    n: uint16 = 300
    c = 0
    for i in range(n):
        c = c + 1
    print("F", c)

    j: uint16 = 0
    c = 0
    for j in range(300):
        c = c + 1
    print("G", c)

    a: int8 = -5
    c = 0
    for i in range(a, 5):
        c = c + 1
    print("H", c)

    c = 0
    for i in range(0, 250, 30):
        c = c + 1
    print("I", c)

    c = 0
    for i in range(250, 0, -30):
        c = c + 1
    print("J", c)

    print("K", trips(250, 30), trips(1000, 1), trips(1000, 7))
    print("L", hundred())
    print("END")

    while True:
        pass


main()
