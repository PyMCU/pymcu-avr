# PyMCU -- str-format: "...".format(x), the pre-f-string way to build a string.
#
# Regression for PyMCU#56. It was rejected with a message about "a nested member
# access (a ZCA field that is itself a ZCA)" -- a program with no string in it at
# all, which sends the reader looking for an attribute they never wrote.
#
# format() IS an f-string written the other way, so it lowers to one. The values
# check the parts an image comparison cannot: that {1}/{0} pick the right arguments
# and that a spec survives.
#
# Expected UART output:
#   2-1
#   hex ff
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    a: uint8 = 1
    b: uint8 = 2
    print("{1}-{0}".format(a, b))
    print("hex {:02x}".format(255))
    print("done")
