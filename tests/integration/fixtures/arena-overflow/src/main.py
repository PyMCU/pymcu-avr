# PyMCU -- arena-overflow: asking for more than the arena reserves raises MemoryError,
# catchable like any other builtin exception. n is far past what any reasonable board
# default or exact-fold reservation would hold.
#
# Expected UART output:
#   overflow
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

n: uint16 = uint16(GPIOR0.value) + 9999
try:
    buf: bytearray = bytearray(n)
    print("allocated")
except MemoryError:
    print("overflow")
print("done")
