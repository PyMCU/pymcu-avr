# PyMCU -- listcomp-over-names: comprehensions whose iterable is a name, and two `for` clauses.
#
# Regression for PyMCU#84. Both of these were rejected with "list comprehensions with a filter
# (if) are not supported ... or drop the filter", for programs containing no `if`: there was no
# filter to drop. Only a literal iterable was ever read, so a comprehension over a NAME fell
# through to the value path, which answers with that message for every comprehension it sees.
# The annotated form gave "List comprehension generated 0 but array is 4" instead.
#
# GPIOR0 reads 0 out of reset, so the indices are 0 and 4.
#
# Expected UART output:
#   2
#   1
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    seed: uint8 = GPIOR0.value

    base = [1, 2, 3, 4]
    doubled: uint8[4] = [x * 2 for x in base]
    print(doubled[seed & 3])

    grid: uint8[9] = [i * j for i in range(3) for j in range(3)]
    print(grid[(seed & 3) + 4])

    print("done")
