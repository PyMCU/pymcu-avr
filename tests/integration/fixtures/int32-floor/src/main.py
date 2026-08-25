# PyMCU -- int32-floor: writing down the smallest int32 there is.
#
# Regression for PyMCU#120. `x: int32 = -2147483648` failed the build, reporting the number
# as out of range for the very type whose minimum it is, and reporting it WITHOUT the minus
# sign so it read as if the program had written a positive value.
#
# The bare literal 2147483648 does not fit a C# int, so the parser stores its 32-bit bit
# pattern, which is -2147483648. The literal range check then applied the minus sign to that
# pattern and got +2147483648, which is genuinely out of range. The sign was applied to a
# number that had already wrapped.
#
# int32 is the widest integer type PyMCU has, so there was no wider annotation to fall back
# on and no cast that avoids the literal. `-2147483647 - 1`, the C idiom, was the workaround
# and is pinned here: nothing that used to build may stop building.
#
# This fixture checks the VALUE, which the unit tests cannot: a compiler could accept the
# literal and still emit the wrong four bytes.
#
# Expected UART output:
#   -2147483648
#   -2147483648
#   2147483647
#   4294967295
#   -32768
#   -128
#   done
from pymcu.hal.console import print
from pymcu.types import int8, int16, int32, uint32


def main():
    lo: int32 = -2147483648
    print(lo)

    same: int32 = -2147483647 - 1
    print(same)

    hi: int32 = 2147483647
    print(hi)

    umax: uint32 = 4294967295
    print(umax)

    i16lo: int16 = -32768
    print(i16lo)

    i8lo: int8 = -128
    print(i8lo)

    print("done")
