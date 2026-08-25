# PyMCU -- uint32-from-float: converting a float into the top half of the uint32 range.
#
# Regression for pymcu-avr#8. The cast lowered to __fixsfsi, which reads its result back as
# a signed int32 and so saturates at 0x80000000: uint32(3000000000.0) came back as
# 2147483648, and so did every larger value. It is the same signed/unsigned split as
# pymcu-avr#7, in the other direction.
#
# Negatives are checked because the unsigned helper has to keep wrapping them the way the
# signed one did: uint32(-100000.0) is int(-100000.0) mod 2**32, which is 4294867296, not 0.
# uint32(-3000000000.0) is the case where the two disagree, 1294967296 against a saturated
# 2147483648.
#
# The narrower casts are here to pin what must NOT change. They keep the signed helper on
# purpose: the value is truncated from the int32 afterwards, and that is what makes
# uint8(-3.5) come out as 253 rather than 0.
#
# Results are printed as integers, not floats, so this fixture does not also depend on
# PyMCU#99.
#
# GPIOR0 is set to 1 and read back as a volatile seed: with literals the constant folder
# answers and the conversion is never emitted.
#
# Expected UART output:
#   3000000000
#   4294967040
#   1294967296
#   4294867296
#   100000
#   -100000
#   253
#   -3
#   31072
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, int8, uint16, uint32, int32


def main():
    GPIOR0.value = 1
    one: uint8 = GPIOR0.value
    f: float = float(one)

    print(uint32(f * 3000000000.0))
    print(uint32(f * 4294967040.0))
    print(uint32(f * -3000000000.0))
    print(uint32(f * -100000.0))

    print(uint32(f * 100000.0))
    print(int32(f * -100000.0))

    print(uint8(f * -3.5))
    print(int8(f * -3.5))
    print(uint16(f * -100000.0))

    print("done")
