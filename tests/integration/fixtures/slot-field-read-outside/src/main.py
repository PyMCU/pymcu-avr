# PyMCU -- slot-field-read-outside: reading a field of a boxed instance from outside the
# class (PyMCU#409).
#
# A class with two or more fields is boxed into a byte slot in SRAM. Reading one of its
# fields from OUTSIDE the class compiled to a read of a flattened `<instance>__<field>`
# variable instead of a load from the slot.
#
# The mangling that claimed it exists to resolve MODULE attribute access: `machine.mem8`
# joins base and member with an underscore to reach the global `machine_mem8`. Instance
# fields are flattened with the same spelling, so `b._t` mangled to `b__t`, a placeholder
# for which always exists, and the branch matched and returned two hundred lines before
# the slot read that knows better could run.
#
# Whether the flattened name holds anything is what decided the answer:
#
#   A   `_pad` is a LITERAL, so the constructor takes the materialising path through
#       __init__, which writes the flattened names on its way to the slot. Read right.
#   B   every field comes straight from a parameter, so the fast path writes the slot and
#       ONLY the slot. Flattened names never written. Read 0.
#
# Two classes of the same shape in one build, one right and one wrong. That is why both
# are here, and why the right one is as much the measurement as the wrong one: a fix that
# broke A would be as wrong as the bug.
#
# Reads through a METHOD are the control. Those always reached the slot, because a method
# receives the slot base address, so a program could print two different values for the
# same field depending on how it asked.
#
# Expected UART output, which is what CPython prints for the same values:
#   a-t 0.1
#   a-pad 3
#   b-t 0.25
#   b-n 7
#   m-a-t 0.1
#   m-b-n 7
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint16


class A:
    def __init__(self, t: float) -> None:
        self._t: float = t
        self._pad: uint16 = 3

    def t(self) -> float:
        return self._t


class B:
    def __init__(self, n: uint16, t: float) -> None:
        self._n: uint16 = n
        self._t: float = t

    def n(self) -> uint16:
        return self._n


a = A(0.1)
b = B(7, 0.25)

uart = UART(9600)


# The reads live in a FUNCTION of their own, not beside the constructions. A module-level
# read is lowered inside the synthesized module-init, where the instance name resolves to
# the slot anyway, so the whole fixture comes out byte-identical and measures nothing. The
# loss needs the read to be compiled somewhere the construction is not, which is where an
# ordinary program reads a sensor object it built at import time.
def report() -> None:
    print("a-t", a._t)
    print("a-pad", a._pad)
    print("b-t", b._t)
    print("b-n", b._n)

    print("m-a-t", a.t())
    print("m-b-n", b.n())


report()

print("done")

while True:
    pass
