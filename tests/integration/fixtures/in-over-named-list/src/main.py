# PyMCU -- in-over-named-list: `x in data` where data is a name bound to a list.
#
# Regression for PyMCU#85. The form was refused by a message whose parenthetical recommends
# exactly that spelling: "requires a list, tuple, set or dict literal (or a name bound to
# one)". Only sets and dicts were actually resolved through their name; a list, annotated or
# not, was not, so the message described a capability that was not there.
#
# GPIOR0 reads 0 out of reset, so seed is 2: in the first list, not in the second.
#
# Expected UART output:
#   yes
#   not in plain
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    seed: uint8 = GPIOR0.value + 2

    data: uint8[4] = [1, 2, 3, 4]
    if seed in data:
        print("yes")
    else:
        print("no")

    plain = [7, 8]
    if seed in plain:
        print("in plain")
    else:
        print("not in plain")

    print("done")
