# RFC 0001 phase 2 -- an outlined ZCA method that CALLS A SIBLING method on self.
#
# Dev.compute() calls self.helper() twice. Previously compute was not outline-safe
# (it touches self via a method call, not just a field), so it was force-inlined at
# every call site -- bloat that grows with both call count and instance count. Now
# compute is outlined ONCE and forwards its own self (here Model A: the self_base
# field param) to the shared Dev_helper body.
#
#   a = Dev(3): compute(1) = helper(1) + helper(2) = (3+1) + (3+2) = 4 + 5 = 9
#   b = Dev(5): compute(2) = helper(2) + helper(3) = (5+2) + (5+3) = 7 + 8 = 15
#
# UART output (9600 baud): "SC\n" banner, then byte 9, then byte 15.
# Correctness proof: 9 and 15 require compute to forward each instance's runtime
# base (3, 5) into the shared helper -- not a baked constant.
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Dev:
    def __init__(self, base: uint8):
        self.base = base

    def helper(self, n: uint8) -> uint8:
        return self.base + n

    def compute(self, n: uint8) -> uint8:
        return self.helper(n) + self.helper(n + 1)


def main():
    uart = UART(9600)
    uart.println("SC")

    a = Dev(3)
    b = Dev(5)

    uart.write(a.compute(1))   # 9
    uart.write(b.compute(2))   # 15

    while True:
        pass
