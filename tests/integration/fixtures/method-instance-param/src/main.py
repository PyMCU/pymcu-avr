# PyMCU -- method-instance-param: a method that takes another instance.
#
# Regression for PyMCU#123. `def read(self, o: C) -> uint8: return self.n + o.n` was compiled
# as a shared outlined body, which cannot receive an instance: a ZCA instance is compile-time
# per-instance, not a runtime value a shared body can take as a parameter. So `self` arrived
# and `o` did not, and the method answered with one operand missing -- 7 where 8 is right.
#
# Free functions with an instance parameter were routed to expansion in #71 and #72; methods
# were left out, which is why this shape kept failing on the default, undecorated path while
# @inline gave the right answer.
#
# The two instances hold different values on purpose: with both at the same value, an answer
# that dropped one operand could still look plausible.
#
# GPIOR0 reads 0 out of reset, so a holds 7, b holds 1, and the sum is 8.
#
# Expected UART output:
#   8
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class C:
    def __init__(self, n: uint8):
        self.n: uint8 = n

    def read(self, o: C) -> uint8:
        return self.n + o.n


def main():
    s: uint8 = GPIOR0.value + 7
    a = C(s)
    b = C(1)
    print(a.read(b))
    print("done")
