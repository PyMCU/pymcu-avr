# PyMCU -- counter-in-loop: a method that updates a field, called in a loop.
#
# Regression for PyMCU#114. A loop body is emitted ONCE and executed many times, but the
# expansion folded the field against the value it held on the way in: `self.n = self.n + 1`
# became `n = 1`, so the most elementary object in programming printed 1, 1, 1 on a clean
# build. It also took the call with it, leaving an empty outlined body.
#
# Expected UART output:
#   1
#   2
#   3
#   sum 6
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
    total: uint16 = 0
    i: uint16 = 0
    while i < 3:
        v: uint16 = c.bump()
        print(v)
        total = total + v
        i = i + 1

    print("sum", total)
    print("done")
