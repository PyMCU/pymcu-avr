# PyMCU -- signed-mixed-width: signed arithmetic and comparisons over mixed widths.
#
# Regressions for PyMCU#92 and PyMCU#94, both answers CPython settles in one line.
#
#   -7 * a // 7   with a = 7 (uint8)   is -7, and printed 9355
#   int8(100) > uint8(200)             is False, and was True
#   int8(100) < 200                    is True, and was False
#
# GPIOR0 and GPIOR1 read 0 out of reset and keep the program off the constant folder: with
# literals the folder answers correctly and the backend is never asked.
#
# Expected UART output:
#   -7
#   x<=y
#   x<200
#   done
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.hal.console import print
from pymcu.types import uint8, int8, int16


def main() -> None:
    a: uint8 = GPIOR0.value + 7
    n: int16 = -7 * a // 7
    print(n)

    x: int8 = int8(GPIOR0.value + 100)
    y: uint8 = uint8(GPIOR1.value + 200)
    if x > y:
        print("x>y")
    else:
        print("x<=y")

    if x < 200:
        print("x<200")
    else:
        print("x>=200")

    print("done")
