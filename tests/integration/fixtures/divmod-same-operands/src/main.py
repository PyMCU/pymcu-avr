# PyMCU -- divmod-same-operands: `//` and `%` over the same dividend and divisor.
#
# Regression for pymcu-avr#10. The two operations are fused into one __div8 call, which
# returns quotient and remainder together, and the guard that kept them apart compared IR
# VALUES: `!Equals(p.Dst, s.Dst)`. Two different IR values can share one register, because the
# allocator reuses a home when live ranges do not overlap, and then the second store clobbered
# the first and BOTH reads answered the remainder. 75 // 10 came back as 5.
#
# GPIOR0 is written and read back as the seed: with a literal the folder answers and no
# division is emitted at all.
#
# Expected UART output:
#   7
#   5
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    GPIOR0.value = 75
    frac: uint8 = GPIOR0.value
    print(frac // 10)
    print(frac % 10)
    print("done")
