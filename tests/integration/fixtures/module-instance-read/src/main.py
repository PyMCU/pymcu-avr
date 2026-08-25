# PyMCU -- module-instance-read: reading a module-level object from another function.
#
# Regressions for PyMCU#126 and PyMCU#127, the two halves that survived the #124 fix, and
# neither needs an interrupt either.
#
# #126 is the read side: a field NEVER assigned outside the constructor read 0 from any
# function other than the one that constructs. That is the ordinary shape of a configuration
# value -- set once, read everywhere -- and adding any write to it was what made it work.
#
# #127 is the write side one level down: `obj.mark()` writes the field through the outline
# write-back convention, so no `obj.n = ...` appears anywhere in the source for the marking
# pass to see. Calling it from main worked; calling it from a function main calls did not.
#
# Expected UART output:
#   5
#   77
#   done
from pymcu.hal.console import print
from pymcu.types import uint8


class Box:
    def __init__(self, n: uint8):
        self.n: uint8 = n

    def mark(self):
        self.n = 77


cfg = Box(5)
obj = Box(0)


def peek() -> uint8:
    return cfg.n


def touch():
    obj.mark()


def main():
    print(peek())
    touch()
    print(obj.n)
    print("done")
