# PyMCU -- base-call-returns-class: a BASE-CLASS call may return a multi-field class instance.
#
# Regression for PyMCU#157. factory-returns-class next door is the same shape one level down,
# a plain function returning a multi-field class, and it worked. Reached through a base-class
# call it did not: the base body vanished from the emitted code and the caller added two slots
# nobody ever wrote, so the answer was 0. The build was clean and nothing was reported.
#
# Three defects, all in the shared emitter for the two spellings of a base call:
#
#   1. A method whose declared return type is a multi-field class is force-inlined, and the
#      definition registered for it is the OUTLINED rewrite, whose receiver arrives as one
#      `self_<field>` parameter per field rather than as `self`. The argument loop skipped only
#      the exact name `self`, so the first ARGUMENT was bound into the receiver's first field,
#      right over the value that had just been copied there, and the callee's own parameter was
#      never bound at all. The body computed raw + raw.
#   2. The assignment target was never registered as the returned class, so the constructor in
#      the base body built into an anonymous slot and the caller read `p_a` / `p_b`, names
#      nothing writes.
#   3. Only a constructor may take the pending construction target as its `self`. Any other
#      method taking it aliased `self` to the target of the ASSIGNMENT the call feeds, so the
#      base body read that object's fields instead of the receiver's.
#
# GPIOR0 carries the seed and the TEST writes it. A literal would fold the whole computation
# and measure the constant folder rather than the call. qemu does not retain a write to GPIOR0,
# so this fixture is only meaningful under the avr8sharp harness.
#
# The two spellings are BOTH here and must agree. They are one construct, and #157's bar is
# that they behave alike: `super().split(raw)` was the silent one, `Base.split(self, raw)` was
# refused outright, and neither was right.
#
# Expected, seed s: raw is s + 5, offset is 10 for Hi and 100 for Lo, so
#
#   hi  = (raw + 10)  + raw
#   lo  = (raw + 100) + raw
#
#   seed s   raw   hi    lo    unfixed
#   0        5     20    110   hi=0  lo=0
#   7        12    34    124   hi=0  lo=0
#   40       45    100   190   hi=0  lo=0
#
# Two DIFFERENT offsets on purpose: an expansion that bound the argument into the receiver's
# field, defect 1 above, computes raw + raw and gives the SAME answer for both, so a fixture
# with one class would have been green over it.
#
# WHAT THIS FIXTURE CANNOT SHOW BY ITSELF. Against the unfixed compiler it does not print 0, it
# fails to BUILD: `Base.split(self, raw)` in Lo was refused outright, and that refusal stops the
# build before Hi's silent zero can be printed. The silent half was measured separately, by
# deleting Lo and running Hi alone through this same harness against the unfixed compiler:
#
#   hi=0 for seeds 0, 7 and 40, where the answers are 20, 34 and 100
#
# and the emitted Hi_widen was two instructions adding Hi_widen.p_a to Hi_widen.p_b, neither of
# which any instruction writes. Keep both spellings here anyway: they are one construct, and
# BothSpellingsAgree is what would catch a future fix landing for only one of them.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.uart import UART
from pymcu.types import uint8, uint16


class Pair:
    def __init__(self, a: uint16, b: uint16):
        self.a: uint16 = a
        self.b: uint16 = b


class Base:
    def __init__(self, offset: uint16):
        self.offset: uint16 = offset

    def split(self, raw: uint16) -> Pair:
        return Pair(raw + self.offset, raw)


class Hi(Base):
    def __init__(self, offset: uint16):
        super().__init__(offset)

    def widen(self, raw: uint16) -> uint16:
        p = super().split(raw)
        return p.a + p.b


class Lo(Base):
    def __init__(self, offset: uint16):
        Base.__init__(self, offset)

    def widen(self, raw: uint16) -> uint16:
        p = Base.split(self, raw)
        return p.a + p.b


def main():
    u = UART(9600)
    raw: uint16 = uint16(GPIOR0.value) + 5

    hi = Hi(10)
    lo = Lo(100)

    u.write_str("hi=")
    u.print_uint16(hi.widen(raw))
    u.write_str("lo=")
    u.print_uint16(lo.widen(raw))

    u.write_str("done\n")
    while True:
        pass


main()
