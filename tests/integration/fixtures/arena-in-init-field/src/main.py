# PyMCU -- arena-in-init-field: `self.buf = bytearray(n)`, the obvious spelling for a
# runtime-sized field, now that PyMCU#392 makes field assignment recognize bytearray()
# at all (previously refused unconditionally, constant size or not).
#
# `self.buf[i]` bracket indexing on this field is deliberately NOT exercised here: it
# currently silently compiles to a bit operation instead of a byte access (PyMCU#418), a
# wrongcode bug distinct from and worse than PyMCU#415 (which at least refuses). This
# fixture verifies construction and the once rule -- an @inline __init__ constructed once
# at module level -- by reading the field's raw value, the offset arena.alloc()
# returned, which is deterministically 0 for the first allocation in the program. See
# arena-in-init (this same directory's sibling fixture) for the local-variable spelling
# this generalizes, kept alongside this one.
#
# Expected UART output:
#   0
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, inline


class Dev:
    @inline
    def __init__(self, n: uint16):
        self.buf = bytearray(n)

    @inline
    def offset(self) -> uint16:
        return self.buf


n: uint16 = uint16(GPIOR0.value) + 3
d: Dev = Dev(n)
print(d.offset())
print("done")
