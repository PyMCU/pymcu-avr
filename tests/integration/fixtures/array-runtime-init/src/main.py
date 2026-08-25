# PyMCU -- array-runtime-init: an array whose initializer elements are not constants.
#
# Regression for PyMCU#81, which reported sum() dropping those elements. The array itself was
# what dropped them: only the folded constants were stored, so `data: uint8[2] = [a, b]` with
# a and b read from registers left both slots at zero. data[0] and data[1] read 0 too, and
# sum() was simply honest about an array of zeros.
#
# GPIOR0 and GPIOR1 read 0 out of reset, so the elements are 3 and 4.
#
# Expected UART output:
#   3
#   4
#   7
#   done
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    a: uint8 = GPIOR0.value + 3
    b: uint8 = GPIOR1.value + 4
    data: uint8[2] = [a, b]
    print(data[0])
    print(data[1])
    print(sum(data))
    print("done")
