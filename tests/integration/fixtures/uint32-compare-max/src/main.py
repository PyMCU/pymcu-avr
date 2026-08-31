# PyMCU -- uint32-compare-max: comparing a uint32 against a constant near the 32-bit
# boundary. The backend rewrites `x <= C` on the false edge as `x > C`, and for a 4-byte
# type it decided "always false, emit nothing" whenever C was int.MaxValue. int.MaxValue is
# the largest SIGNED 32-bit value; a uint32 goes twice as high, so the comparison was
# dropped for a range of x where it is not decided at all.
#
# The seed comes from GPIOR0 so nothing here is known at compile time.
#   seed 10 -> x = 1_000_000_000  (below 2**31-1)
#   seed 30 -> x = 3_000_000_000  (above 2**31-1, and still a valid uint32)
#
# Outputs, 1 for the then-branch and 2 for the else-branch:
#   GPIOR1 = 0x4A   x <= 2147483647          the boundary that was wrong
#   GPIOR2 = 0x4B   x >  2147483647          the same question, other spelling
#   OCR0A  = 0x47   y <= 65535 (uint16)      the same code path, correct at 16 bits
#   OCR0B  = 0x48   x <= 1500000000          a threshold below the boundary
#
from pymcu.types import uint8, uint16, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, OCR0A, OCR0B


def main():
    seed: uint8 = GPIOR0.value
    x: uint32 = uint32(seed) * 100000000

    if x <= 2147483647:
        GPIOR1.value = 1
    else:
        GPIOR1.value = 2

    if x > 2147483647:
        GPIOR2.value = 2
    else:
        GPIOR2.value = 1

    y: uint16 = uint16(seed) * 1000
    if y <= 65535:
        OCR0A.value = 1
    else:
        OCR0A.value = 2

    if x <= 1500000000:
        OCR0B.value = 1
    else:
        OCR0B.value = 2

    asm("BREAK")

    while True:
        pass
