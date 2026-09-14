# PyMCU -- range-surface: reversed(range()), x in range(), enumerate(range()) at run time,
# and a comprehension with a step (PyMCU#287, PyMCU#288)
#
# `lo` and `x` come from GPIOR0 (the test seeds 5) so nothing here folds to a constant.
#
# Expected UART (115200), seed 5:
#   A 2 / A 1 / A 0
#   B 6 / B 4 / B 2
#   C in
#   D notin
#   E odd
#   F 0 5 / F 1 6 / F 2 7
#   G 0 / G 2 / G 4 / G 6 / G 8
#   H 4 / H 3 / H 2 / H 1 / H 0
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    seed: uint8 = GPIOR0.value
    x: uint8 = seed
    lo: uint8 = seed

    for i in reversed(range(3)):
        print("A", i)

    for i in reversed(range(2, 8, 2)):
        print("B", i)

    if x in range(10):
        print("C in")
    if x not in range(6, 10):
        print("D notin")
    if x in range(1, 10, 2):
        print("E odd")

    for k, v in enumerate(range(lo, lo + 3)):
        print("F", k, v)

    xs: uint8[5] = [i for i in range(0, 10, 2)]
    for v in xs:
        print("G", v)

    for i in reversed(range(lo)):
        print("H", i)

    print("END")

    while True:
        pass


main()
