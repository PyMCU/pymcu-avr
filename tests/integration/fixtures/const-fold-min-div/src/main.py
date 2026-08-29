# PyMCU -- const-fold-min-div: MIN // -1 folded, next to MIN // -1 executed.
#
# Regression for PyMCU#223. Folding int32 MIN // -1 or MIN % -1 at compile time threw a .NET
# OverflowException that reached the user as `InternalCompilerError` at line 1:1, because the
# true quotient 2147483648 does not fit the int the fold was computed in. int8 and int16 never
# hit it: their quotients, 128 and 32768, fit and then wrapped correctly.
#
# Every value is printed TWICE: once folded from literals, once computed at run time from the
# same numbers made opaque by a volatile seed. The two columns have to agree. That is the whole
# point of the fixture -- the answer the folder gives is not a reading of Python's semantics,
# it is whatever the chip already produces, and the two must not drift apart again.
#
# GPIOR0 is set to 1 and read back: without that the run-time column would be folded too and
# the fixture would be comparing the folder against itself.
#
# Expected UART output, folded and run-time alternating:
#   -128
#   -128
#   0
#   0
#   -32768
#   -32768
#   0
#   0
#   -2147483648
#   -2147483648
#   0
#   0
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, int8, int16, int32


def main():
    GPIOR0.value = 1
    one: uint8 = GPIOR0.value

    f8q: int8 = -128 // -1
    a8: int8 = -128 * one
    b8: int8 = 0 - one
    r8q: int8 = a8 // b8
    print(f8q)
    print(r8q)

    f8r: int8 = -128 % -1
    r8r: int8 = a8 % b8
    print(f8r)
    print(r8r)

    f16q: int16 = -32768 // -1
    a16: int16 = -32768 * one
    b16: int16 = 0 - one
    r16q: int16 = a16 // b16
    print(f16q)
    print(r16q)

    f16r: int16 = -32768 % -1
    r16r: int16 = a16 % b16
    print(f16r)
    print(r16r)

    f32q: int32 = -2147483648 // -1
    a32: int32 = -2147483648 * one
    b32: int32 = 0 - one
    r32q: int32 = a32 // b32
    print(f32q)
    print(r32q)

    f32r: int32 = -2147483648 % -1
    r32r: int32 = a32 % b32
    print(f32r)
    print(r32r)

    print("done")
