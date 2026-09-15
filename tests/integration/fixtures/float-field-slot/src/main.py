# PyMCU -- float-field-slot: a float field alongside a second field (PyMCU#404).
#
# A class with ONE field collapses to a scalar. With two or more it is boxed into a byte
# slot in SRAM, and the constructor splits each field into its bytes to store it. That
# split was an arithmetic shift -- `value >> 8`, `>> 16`, `>> 24` -- and a float's bytes
# are its IEEE-754 representation, not an arithmetic quantity. The AVR backend refused:
#
#   the AVR backend has no float lowering for RShift. Reaching here with an arithmetic
#   or bitwise operator means an earlier pass rewrote a float operation into one, which
#   is a bug in that pass
#
# It named the constructor's line and a pass rather than a cause, and it refused an
# ordinary class: a driver that keeps a timeout alongside a pin count has this shape.
#
# A float field is now reinterpreted as 32 bits before the split, which is what the READ
# side has always done: a multi-byte field comes back through one typed load over the
# same four bytes. So the ROUND TRIP is what this fixture measures, and it is read back
# THROUGH A METHOD, because that is the path that goes to the slot: a direct `a._t` from
# outside the class reads a flattened variable instead, which is a separate bug and would
# measure the wrong thing here.
#
# The field ORDER is varied, because the float's offset inside the slot is what changes
# between the two, and a build that stored three of the four bytes would pass a build
# check and print a plausible wrong number.
#
# Expected UART output:
#   gt 1
#   t 0.1
#   pad 3
#   rev-gt 1
#   rev-t 0.25
#   rev-n 7
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint16


class A:
    def __init__(self, t: float) -> None:
        self._t: float = t
        self._pad: uint16 = 3

    def gt(self) -> uint16:
        if self._t > 0.05:
            return 1
        return 0

    def t(self) -> float:
        return self._t

    def pad(self) -> uint16:
        return self._pad


class B:
    # The same two fields the other way round, so the float sits at a non-zero offset.
    def __init__(self, n: uint16, t: float) -> None:
        self._n: uint16 = n
        self._t: float = t

    def gt(self) -> uint16:
        if self._t > 0.2:
            return 1
        return 0

    def t(self) -> float:
        return self._t

    def n(self) -> uint16:
        return self._n


a = A(0.1)
b = B(7, 0.25)


def main() -> None:
    uart = UART(9600)

    print("gt", a.gt())
    print("t", a.t())
    print("pad", a.pad())

    print("rev-gt", b.gt())
    print("rev-t", b.t())
    print("rev-n", b.n())

    print("done")
    while True:
        pass
