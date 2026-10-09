# expect: divergence https://docs.pymcu.org/limitations/#heap-objects (a program whose static storage leaves the GC heap genuinely exhausted)
# doc: https://docs.pymcu.org/limitations/
"""CPython's list growth is unbounded and never fails. On AVR the GC heap sits
in whatever SRAM is left after static storage (lib/src/pymcu's bump allocator,
gc_runtime.S): a module-level array sized to use nearly all of an ATmega328P's
2048 bytes leaves only a few bytes of heap for the rest of THIS exact program
-- a genuine, measured exhaustion (not a size gc_alloc's own 255-byte object
ceiling would refuse outright regardless of free space; see probe 840 for
that one).

Regression probe for PyMCU-gcnull: before the fix, a doubly-failed
allocation (OOM, then OOM again after the collection attempt gc_alloc makes
internally) wrote the next list's header through the returned null pointer
unchecked -- landing at SRAM address 0x0000, the register file on AVR
(R0/R1 included; R1 is the AVR "always zero" register this backend's own
generated code relies on for every 16-bit comparison, so corrupting it is
silent and permanent, not a crash). Caught here instead: a MemoryError.
"""
from pymcu.types import uint8

pad: uint8[1957] = [0] * 1957

try:
    y = []
    y.append(1)
    print(len(y))
except MemoryError:
    print("MemoryError")
print("END")
