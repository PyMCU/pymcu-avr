# PyMCU -- bool-protocol: __bool__ in every position that asks for a truth value.
#
# Regression for PyMCU#121. __bool__ was consulted when the instance was the WHOLE condition
# of an if or a while, and nowhere else: in a conditional expression, under `not`, and as an
# operand of `and` or `or`, the raw instance handle was tested instead. The handle is not the
# object's truth value, so an instance whose __bool__ says true came out false.
#
# The value is chosen so every line answers TRUE. A test built around a false answer would
# have passed throughout the bug, which is exactly how #98's table came to list __bool__ as
# healthy.
#
# GPIOR0 reads 0 out of reset, so a is 9 and `a > 4` is true.
#
# Expected UART output:
#   if yes
#   1
#   not: yes
#   and: yes
#   or: yes
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class Flag:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def __bool__(self) -> bool:
        return self.a > 4


def main():
    x = Flag(GPIOR0.value + 9)

    if x:
        print("if yes")
    else:
        print("if no")

    print(1 if x else 0)

    if not x:
        print("not: no")
    else:
        print("not: yes")

    if x and True:
        print("and: yes")
    else:
        print("and: no")

    if x or False:
        print("or: yes")
    else:
        print("or: no")

    print("done")
