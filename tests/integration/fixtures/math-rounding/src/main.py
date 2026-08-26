# PyMCU -- math-rounding: math.floor / math.ceil / math.trunc on a run-time float.
#
# The three did not exist before #174. This checks what they compute, on both sides of zero
# and on both whole and fractional values.
#
# The value comes from GPIOR0, not from a literal: a literal folds, and the fixture would
# measure the constant folder rather than the functions.
#
#   x = (seed - 4) / 2      seed 0..8  ->  -2.0 -1.5 -1.0 -0.5 0.0 0.5 1.0 1.5 2.0
#
# The three answers are read back through the CPU into GPIORs, four bits each, since every
# result is in -3..3. Two's complement in four bits, so the test sign-extends.
#
# Data-space addresses (ATmega328P): GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, int32, uint8
import math


def main():
    seed: uint8 = GPIOR0.value
    x: float = (float(seed) - 4.0) / 2.0

    f: int32 = math.floor(x)
    c: int32 = math.ceil(x)
    t: int32 = math.trunc(x)

    GPIOR1.value = uint8((f & 0x0F) | ((c & 0x0F) << 4))
    GPIOR2.value = uint8(t & 0x0F)

    asm("BREAK")
    while True:
        pass
