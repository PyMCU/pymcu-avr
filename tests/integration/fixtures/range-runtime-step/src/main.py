# PyMCU -- range-runtime-step: a signed runtime step picks its direction at run time (PyMCU#286)
#
# The exit test of a range loop was chosen at compile time from a constant step only. A step
# held in a variable always got the ascending test, so range(10, 0, step) with step = -2
# exited before its first iteration.
#
# Expected UART (115200):
#   A 5 10
#   B 0 0
#   C 10 2
#   END
from pymcu.hal.console import print
from pymcu.types import int8, uint16


def down(step: int8) -> uint16:
    c: uint16 = 0
    for i in range(10, 0, step):
        c = c + 1
    return c


def up(step: int8) -> uint16:
    c: uint16 = 0
    for i in range(0, 10, step):
        c = c + 1
    return c


def main():
    print("A", down(-2), down(-1))
    print("B", down(1), up(-1))
    print("C", up(1), up(7))
    print("END")

    while True:
        pass


main()
