# PyMCU -- module-array-survives: a module-level array keeps the bytes it reserved.
#
# Regression for PyMCU#146. Dead-global elimination built its live set from Variable uses,
# write destinations, ArrayBase operands and inline-asm text, but NOT from the array NAME on a
# load or a store. An array reached only through a subscript therefore looked unreferenced and
# its reservation was removed -- while the loads and stores that use it were left in place, so
# the program kept writing bytes that were no longer reserved and the allocator handed those
# same bytes to temporaries and to an outlined call's parameter slot.
#
# The shape that showed it: deleting the single line `print(total())` made the program correct,
# because without that call there was no outlined parameter to collide with.
#
# GPIOR0 reads 0 out of reset, so the elements are 10, 20 ... 80.
#
# Expected UART output:
#   70
#   360
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

buf: uint8[8] = [0, 0, 0, 0, 0, 0, 0, 0]


def fill(seed: uint8):
    i: uint8 = 0
    while i < 8:
        buf[i] = seed + i * 10
        i = i + 1


def total() -> uint16:
    t: uint16 = 0
    i: uint8 = 0
    while i < 8:
        t = t + buf[i]
        i = i + 1
    return t


def main():
    fill(GPIOR0.value + 10)
    print(buf[6])
    print(total())
    print("done")
