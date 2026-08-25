# PyMCU -- module-array-name-collision: a module array keeps its own size.
#
# Regression for PyMCU#167. A function's own local array was registered under the BARE name,
# so it overwrote a module-level array of the same name in the size registry. The AVR UART HAL
# declares `buf: uint8[32]` inside uart_write_fmt, and this program declares `buf: uint8[300]`
# at module level, which is about the most likely name anyone picks for a buffer.
#
# The consequence was silent and value-wrong, not merely wrong metadata. A store written inside
# a function carried count 32, so the backend chose the narrow 8-bit index path, and every write
# past index 255 wrapped into the low bytes. The READ in main carried 300 and used the wide path,
# so the two halves of one array disagreed about how wide the index is.
#
# The array is named `buf` ON PURPOSE. Renaming it to anything the stdlib does not use makes the
# program correct, which is how the collision was pinned, so a future reader must not "tidy" the
# name: it is the measurement.
#
# `global` is not incidental either. Passing a fixed array to a function is refused ("pass an
# array to a function as a 'bytearray'"), so `global` is the spelling left for writing a
# module-level array from a function.
#
# GPIOR0 is zero out of reset and only defeats folding, so these are run-time indices straddling
# the 256-byte boundary.
#
# Expected UART output:
#   99
#   77
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

buf: uint8[300] = bytearray(300)


def fill(i: uint16, v: uint8):
    global buf
    buf[i] = v


def main():
    seed: uint16 = GPIOR0.value
    fill(257 + seed, 99)
    fill(299 - seed, 77)
    print(buf[257 + seed])
    print(buf[299 - seed])
    print("done")
    while True:
        pass


main()
