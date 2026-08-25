# PyMCU -- module-instance-field: a module-level object mutated from another function.
#
# Regression for PyMCU#124. `obj = Box(0)` at module level, `obj.n = 77` in one function and
# `print(obj.n)` in another printed 0. The field had no storage: the write was a dead store to
# a name nothing else in that function read, and the reader folded the constructor's value,
# because functions are lowered in an order the program does not control.
#
# Writing and reading in the SAME function always worked, which is what made this look like an
# interrupt problem when it was found -- it needs no interrupt at all.
#
# `flat` is the control: a plain module-level global in the same shape was always correct, and
# it is here so a regression can be told apart from the whole file failing.
#
# Expected UART output:
#   77
#   5
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


class Box:
    def __init__(self, n: uint8):
        self.n: uint8 = n


obj = Box(0)
flat: uint8 = 0


def setup():
    global flat
    obj.n = 77
    flat = 5


def main():
    setup()
    print(obj.n)
    print(flat)
    print("done")
