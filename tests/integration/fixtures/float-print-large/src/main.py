# PyMCU -- float-print-large: printing a float bigger than the old accumulator held.
#
# Regression for PyMCU#99. uart_write_float scaled the whole value by 100 into a uint32
# before producing a digit, which caps what can be printed at 2**32 / 100. Every float past
# that came out as the same saturated number: 1e8 and 1e9 both printed 21474836.48, and so
# did 3e9. On AVR the cap landed lower still, at 2**31 / 100, because uint32(float) lowered
# to the signed helper (pymcu-avr#8).
#
# The small values are here because the fix changes how every float is printed, not only the
# large ones. 0.999 and 9.999 are the rounding-carry cases: the rounding has to leave the
# fraction and land on the integer part, so they are 1.0 and 10.0, never 0.100 and 9.100.
#
# Everything stays below 2**31 on purpose. Above that the answer also depends on
# pymcu-avr#8, which uint32-from-float covers, and this fixture is about the printer.
#
# GPIOR0 is set to 1 and read back as a volatile seed: with literals the constant folder
# answers and the multiply is never emitted.
#
# Expected UART output:
#   3.5
#   0.75
#   -7.0
#   0.01
#   1.0
#   10.0
#   0.0
#   1000000.0
#   20000000.0
#   100000000.0
#   1000000000.0
#   -1000000000.0
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    GPIOR0.value = 1
    one: uint8 = GPIOR0.value
    f: float = float(one)

    print(f * 3.5)
    print(f * 0.75)
    print(f * -7.0)
    print(f * 0.005)
    print(f * 0.999)
    print(f * 9.999)
    print(f * 0.0)

    print(f * 1000000.0)
    print(f * 20000000.0)
    print(f * 100000000.0)
    print(f * 1000000000.0)
    print(f * -1000000000.0)

    print("done")
