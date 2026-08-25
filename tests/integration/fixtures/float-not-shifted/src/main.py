# PyMCU -- float-not-shifted: a float multiplied or divided by a power of two.
#
# Regression for pymcu-avr#5 and PyMCU#128. The IR optimizer's power-of-two rewrites were
# applied without checking that the operands were integers, so a float reached AvrCodeGen
# as a shift or a mask:
#
#   x * 2^n  -> x << n       "Float comparison op LShift not supported"
#   x // 2^n -> x >> n       "Float comparison op RShift not supported"
#   x // 1   -> x            3.5 // 1 printed 3.5 instead of 3.0, with no diagnostic
#
# The first two aborted the build. The third is the one that mattered: a different number
# and nothing said so. A float's bit pattern is not its magnitude, so halving it is not a
# shift, and its floored division by 1 is not itself.
#
# The negatives are here because the floor has to round toward -inf: -3.5 // 2 is -2.0 and
# -3.5 // 1 is -4.0, which is what the real float path gives and what a shift never would.
#
# The identities are pinned too, since they share the routine and must keep working:
# x * 1, x * 0, x + 0 and x - 0.
#
# The last pair is the folded shape from pymcu-avr#5's original report. There the power of
# two is the OTHER operand: c is a compile-time constant, so `float(c) * 3.0` folded to a
# multiply by 512, and 512 is what got strength-reduced onto the float. Only c differs
# between the two lines, and 500 always worked while 512 did not.
#
# The integer lines are the point of the rewrites in the first place, so they are pinned to
# prove the guard did not disable them: 968 * 4, 968 // 8 and 968 % 16 must still compile to
# shifts and a mask, not to a call into the division runtime.
#
# GPIOR0 and GPIOR1 are set and read back as volatile seeds. Note that a reproducer WITHOUT
# a volatile seed measures the constant folder rather than the backend, which is how the
# original report came to describe the scope wrongly: it concluded that multiplying a
# run-time float never worked, when what fails is a power-of-two integer literal.
#
# Expected UART output:
#   3.5
#   7.0
#   28.0
#   1.0
#   3.0
#   3.5
#   0.0
#   3.5
#   3.5
#   10.5
#   -3.5
#   -7.0
#   -2.0
#   -4.0
#   1536.0
#   1500.0
#   968
#   3872
#   121
#   8
#   done
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.hal.console import print
from pymcu.types import uint8, uint16


def main():
    GPIOR0.value = 7
    a: uint8 = GPIOR0.value

    fb: float = float(a) / 2.0          # 3.5
    print(fb)
    print(fb * 2)
    print(fb * 8)
    print(fb // 2)
    print(fb // 1)

    print(fb * 1)
    print(fb * 0)
    print(fb + 0)
    print(fb - 0)
    print(fb * 3)

    n: float = float(a) / -2.0          # -3.5
    print(n)
    print(n * 2)
    print(n // 2)
    print(n // 1)

    folded: uint16 = 512
    print(float(folded) * 3.0)
    unfolded: uint16 = 500
    print(float(unfolded) * 3.0)

    GPIOR1.value = 200
    lo: uint8 = GPIOR1.value
    w: uint16 = a // 7 * 768 + lo       # 968
    print(w)
    print(w * 4)
    print(w // 8)
    print(w % 16)

    print("done")
