# A float whose only use is a COMPARISON is still a float (PyMCU#388).
#
# The set that decides which register layout a float parameter is spilled with at function
# entry was built by walking only the instructions that PRODUCE a value: a binary op, a copy,
# a return, a call. A parameter whose every use is a comparison was therefore never seen as a
# float. It was spilled with the R24-anchored uint32 layout and read back by the float path
# with the R22-anchored C layout, so the two 16-bit halves swapped and the comparison decided
# on a number nobody wrote: every `>` answered false and every `<` answered true, for every
# pair of values.
#
# The same comparison one float add later answered correctly, because the add had rewritten
# the slot in the layout the comparison reads -- which is why `self._t + 0.0 > 0.05` was right
# where `self._t > 0.05` was wrong, and why this survived fixtures/float-compare, whose
# operands are subtractions.
#
# Every line below is a relation whose answer the values decide, both ways round, so a
# comparison that ignores its operands cannot pass. The runtime pair comes from GPIOR0, which
# reads 0 out of reset, so those two cannot be folded.
#
# Expected UART output, which is what CPython prints for the same program:
#   field-gt 1
#   field-ge 1
#   field-lt 1
#   field-via-local 1
#   field-plus-zero 1
#   param-gt-true 1
#   param-gt-false 0
#   param-gt-big 1
#   param-lt-false 0
#   param-lt-big 0
#   runtime-gt 1
#   runtime-gt-reversed 0
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint16


class Timeout:
    # One float field, read only by comparisons. Outlined, its field becomes a float
    # parameter of the shared body -- the shape the driver's `> self._timeout` has.
    def __init__(self, t: float) -> None:
        self._t: float = t

    def gt(self) -> uint16:
        if self._t > 0.05:
            return 1
        return 0

    def ge(self) -> uint16:
        if self._t >= 0.05:
            return 1
        return 0

    def lt(self) -> uint16:
        if self._t < 1.0:
            return 1
        return 0

    def via_local(self) -> uint16:
        v: float = self._t
        if v > 0.05:
            return 1
        return 0

    def plus_zero(self) -> uint16:
        # The control: one float add ahead of the comparison was always right.
        if self._t + 0.0 > 0.05:
            return 1
        return 0


def gt(t: float, k: float) -> uint16:
    if t > k:
        return 1
    return 0


def lt(t: float, k: float) -> uint16:
    if t < k:
        return 1
    return 0


t = Timeout(0.1)


def main() -> None:
    seed: uint16 = GPIOR0.value
    x: float = (seed + 100) * 0.001
    y: float = (seed + 50) * 0.001
    print("field-gt", t.gt())
    print("field-ge", t.ge())
    print("field-lt", t.lt())
    print("field-via-local", t.via_local())
    print("field-plus-zero", t.plus_zero())
    print("param-gt-true", gt(0.1, 0.05))
    print("param-gt-false", gt(0.1, 0.2))
    print("param-gt-big", gt(2.0, 1.0))
    print("param-lt-false", lt(0.1, 0.05))
    print("param-lt-big", lt(2.0, 1.0))
    print("runtime-gt", gt(x, y))
    print("runtime-gt-reversed", gt(y, x))
    print("END")
    while True:
        pass
