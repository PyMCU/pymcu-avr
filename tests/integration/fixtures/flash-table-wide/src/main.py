# PyMCU -- flash-table-wide: a const flash table wider than a byte is emitted and read.
#
# Regression for PyMCU#135. Only const[uint8[N]] was recognised as a flash array. A table
# whose elements are wider fell through: the name was never registered as an array, so T[i]
# lowered to a REGISTER BIT TEST on a scalar that does not exist. Every read folded to zero,
# the build was clean, and nothing warned. With a run-time index the same path could not fold
# the index and failed the build with "Bit index must be constant for reading", a sentence
# about register bits in a program that has none.
#
# A 16-bit calibration or period table in flash is one of the most ordinary things on an
# 8-bit part, precisely because the values do not fit in a byte.
#
# The signed entries matter: elements are stored little-endian and reassembled by shifting
# what is already there up by eight and ORing the next byte in, so -5 has to come back as -5
# rather than 65531. GPIOR0 is zero out of reset and only defeats constant folding, so the
# last read exercises the run-time index that used to fail the build.
#
# Expected UART output:
#   300
#   300
#   -5
#   70000
#   66
#   65
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, int16, uint32, const

U16: const[uint16[3]] = [65, 300, 1000]
I16: const[int16[3]] = [-5, 300, -1000]
U32: const[uint32[3]] = [7, 70000, 3]
U8: const[uint8[3]] = [65, 66, 67]
T: const[uint16[4]] = [65, 100, 125, 150]


def main():
    print(U16[1])
    print(I16[1])
    print(I16[0])
    print(U32[1])
    print(U8[1])
    i: uint8 = GPIOR0.value & 3
    print(T[i])
    print("done")
    while True:
        pass


main()
