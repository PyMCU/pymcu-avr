# PyMCU -- arena-in-init-field: `self.buf = bytearray(n)`, the obvious spelling for a
# runtime-sized field, now that PyMCU#392 makes field assignment recognize bytearray()
# at all (previously refused unconditionally, constant size or not).
#
# Writes and reads back three indices through `self.buf[i]` / `d.buf[i]` (PyMCU#418: a
# field holding an arena buffer was never marked as one at the field-write site, so
# indexing it fell through to the generic bit-index fallback and silently compiled to a
# bit operation instead of a byte access -- fixed on this branch, not just documented),
# and reports `len(self.buf)`. See arena-in-init (this same directory's sibling fixture)
# for the local-variable spelling this generalizes, kept alongside this one.
#
# Expected UART output:
#   11
#   22
#   33
#   4
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16, inline


class Dev:
    @inline
    def __init__(self, n: uint16):
        self.buf = bytearray(n)

    @inline
    def poke(self, i: uint16, v: uint8) -> None:
        self.buf[i] = v

    @inline
    def peek(self, i: uint16) -> uint8:
        return self.buf[i]

    @inline
    def size(self) -> uint16:
        return len(self.buf)


n: uint16 = uint16(GPIOR0.value) + 4
d: Dev = Dev(n)
d.poke(0, 11)
d.poke(1, 22)
d.poke(2, 33)
print(d.peek(0))
print(d.peek(1))
print(d.peek(2))
print(d.size())
print("done")
