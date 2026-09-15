# PyMCU -- arena-in-init: a runtime-sized allocation inside an @inline __init__
# constructed once at module level.
#
# `self.buf: bytearray = bytearray(n)` (a per-instance FIELD) does not compile yet on any
# target -- a separate, pre-existing ZCA field-construction gap this feature found and
# does not fix (docs/rfcs/0004-arena-allocator.md section 6: `self.buf = bytearray(4)`
# fails identically, constant size or not, with none of the arena changes present).
#
# What this fixture proves instead is the once rule itself: a LOCAL bytearray(n) inside
# an @inline __init__ is accepted, because currentFunction stays "main" through the
# inlining exactly as it would for a bare module-level statement (RFC section 2). The
# offset arena.alloc() returned is stored in an ordinary (non-bytearray-typed) field,
# which is unaffected by the field-construction gap. This is the first allocation the
# program makes, from a fresh arena, so the offset is deterministically 0.
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
        buf: bytearray = bytearray(n)
        self.off = buf

    @inline
    def first_offset(self) -> uint16:
        return self.off


n: uint16 = uint16(GPIOR0.value) + 3
d: Dev = Dev(n)
print(d.first_offset())
print("done")
