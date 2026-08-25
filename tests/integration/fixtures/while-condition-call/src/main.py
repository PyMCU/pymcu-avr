# PyMCU -- while-condition-call: a method that updates a field, called in the CONDITION.
#
# Regression for PyMCU#118. The condition of a `while` is evaluated once per iteration, but
# it was lowered with the constants the loop was entered with, so `c.bump()` folded to the 1
# it returned the first time. The comparison vanished, `JMP` back to the top was made
# unconditional, the outlined body was emitted empty, and everything after the loop was
# dropped from the image.
#
# Expected UART output:
#   tick
#   tick
#   tick
#   99
#   done
from pymcu.hal.console import print
from pymcu.types import uint16


class Counter:
    def __init__(self):
        self.n: uint16 = 0

    def bump(self) -> uint16:
        self.n = self.n + 1
        return self.n


def main():
    c = Counter()
    while c.bump() < 4:
        print("tick")

    print(99)
    print("done")
