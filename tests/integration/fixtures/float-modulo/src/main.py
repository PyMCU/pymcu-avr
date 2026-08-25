# PyMCU -- float-modulo: the remainder of two floats.
#
# Regression for pymcu-avr#9. `%` between floats had no lowering at all: it reached
# AvrCodeGen's comparison branch and aborted the build with "Float comparison op Mod not
# supported", a message that names a comparison for a modulo.
#
# The four sign combinations are the point of the fixture. Python's float `%` is FLOORED,
# matching its `//`: the result takes the sign of the DIVISOR. fmodf is TRUNCATED and takes
# the sign of the dividend, so two of the four need a correction and two do not:
#
#    3.5 %  2.0 ->  1.5     fmodf agrees
#   -3.5 %  2.0 ->  0.5     fmodf gives -1.5
#    3.5 % -2.0 -> -0.5     fmodf gives  1.5
#   -3.5 % -2.0 -> -1.5     fmodf agrees
#
# A truncating implementation passes the first and last and fails the middle two, which is
# why all four are here rather than one.
#
# 1e9 % 3.0 is the precision line. It is 1.0, and it is what rules out implementing this as
# x - floor(x / y) * y: in float32 that identity gives 0.0, because 1e9 / 3.0 does not fit
# the 24-bit mantissa and the error survives the multiply back. fmodf is exact.
#
# The integer-literal divisors reach this path only since the optimizer stopped rewriting
# x % 2^n into a mask on floats (PyMCU#128). Before that fix, `p % 2` aborted with a float
# BitAnd and `p % 1` silently printed 0.0.
#
# GPIOR0 is set to 7 and read back as a volatile seed: with literals the constant folder
# answers and no modulo is emitted.
#
# Expected UART output:
#   1.5
#   0.5
#   -0.5
#   -1.5
#   1.5
#   0.5
#   0.5
#   0.0
#   0.5
#   1.0
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    GPIOR0.value = 7
    a: uint8 = GPIOR0.value

    p: float = float(a) / 2.0           # 3.5
    n: float = float(a) / -2.0          # -3.5
    two: float = float(a) / 3.5         # 2.0
    mtwo: float = float(a) / -3.5       # -2.0

    print(p % two)
    print(n % two)
    print(p % mtwo)
    print(n % mtwo)

    print(p % 2)
    print(p % 1)
    print(n % 1)

    print(p % p)

    print(p % 0.75)

    big: float = float(a) / 7.0 * 1000000000.0      # 1e9
    print(big % 3.0)

    print("done")
