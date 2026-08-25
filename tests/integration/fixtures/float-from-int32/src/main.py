# PyMCU -- float-from-int32: converting a 32-bit integer to float.
#
# Regression for pymcu-avr#7. The conversion loaded only the low 16 bits of the source and
# CLRed the top half away, so float(100000) came back as 34464.0, which is 100000 & 0xFFFF.
# Every 32-bit value above 65535 and every negative int32 became a different number, with no
# diagnostic, while the integer itself printed correctly.
#
# The unsigned case needs the unsigned helper as well: __floatsisf reads its argument as
# signed, so a uint32 above 2^31 converted to a negative float.
#
# The value above 2^31 is checked with a comparison rather than a print, because printing it
# runs into PyMCU#99: print() saturates every float above 21474836.48. The comparison is what
# the unsigned helper actually changes -- with the signed one the result is negative.
#
# GPIOR0 is set to 1 and read back as a volatile seed: with literals the constant folder
# answers and the conversion is never emitted.
#
# Expected UART output:
#   100000
#   100000.0
#   -100000.0
#   positive
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint32, int32


def main():
    GPIOR0.value = 1
    one: uint8 = GPIOR0.value

    big: uint32 = 100000 * one
    print(big)
    print(float(big))

    neg: int32 = -100000 * one
    print(float(neg))

    huge: uint32 = 3000000000 * one
    if float(huge) > 2000000000.0:
        print("positive")
    else:
        print("negative")

    print("done")
