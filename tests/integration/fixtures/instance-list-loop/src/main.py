# PyMCU -- instance-list-loop: iterating a name bound to a list of instances.
#
# Regression for PyMCU#100. `for o in objs`, where objs is a name bound to a list of class
# instances, compiled clean and read every field as zero. Nothing ever constructed the
# elements, and the loop variable named fields that had never been written. Writing the same
# literal straight into the `for` is rejected with "elements must be compile-time integer
# constants", so the shape that failed silently was the one that looked most ordinary.
#
# GPIOR0 is the seed that keeps the program off the constant folder; it reads 0 out of reset,
# so s is 4 and the two instances answer 5 and 6.
#
# Expected UART output:
#   5
#   6
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class A:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def g(self) -> uint8:
        return self.a + 1


def main() -> None:
    s: uint8 = GPIOR0.value + 4
    objs = [A(s), A(s + 1)]
    for o in objs:
        print(o.g())

    print("done")
