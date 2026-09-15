# PyMCU -- arena-module-level: bytearray(n) at module level, runtime n via GPIOR0.
#
# n is not a compile-time constant (GPIOR0 reads 0 out of reset, so n = 5), which the
# compiler proves runs at most once here: a module-level statement, not inside a loop.
# Writes through the buffer, reads back, and reports its length. See
# docs/rfcs/0004-arena-allocator.md (PyMCU repo).
#
# Expected UART output:
#   11
#   22
#   5
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

n: uint16 = uint16(GPIOR0.value) + 5
buf: bytearray = bytearray(n)
buf[0] = 11
buf[1] = 22
print(buf[0])
print(buf[1])
print(len(buf))
print("done")
