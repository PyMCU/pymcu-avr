# PyMCU -- float-div-zero: dividing a float by zero raises, like Python and like the ints.
#
# Regression for PyMCU#151. The float branch of the binary lowering returned before the
# divide-by-zero guard the INTEGER path has always had, so a float division by zero produced
# an infinity instead of raising ZeroDivisionError. Worse than a wrong number: print() takes
# the integer part of a float through uint32(), and an infinity converted to an integer is
# zero, so an infinity reached the port as "0.0". A reading of inf announces itself; a reading
# of 0.0 looks like the sensor is idle.
#
# The two widths of the same operation also disagreed: `a // 0` with a run-time zero raised and
# was catchable, `p / 0.0` did not.
#
# The test seeds GPIOR0, so these are run-time values and not folded. Note that a literal zero
# divisor is now a COMPILE error, on both paths, which is why the zero here is built by
# multiplying rather than written.
#
# Expected UART output (GPIOR0 seeded to 7):
#   caught
#   caught-mod
#   3.5
#   1.75
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    a: uint8 = GPIOR0.value
    p: float = float(a) / 2.0
    z: float = float(a) * 0.0

    try:
        q: float = p / z
        print(q)
    except ZeroDivisionError:
        print("caught")

    try:
        r: float = p % z
        print(r)
    except ZeroDivisionError:
        print("caught-mod")

    # A divisor that is not zero still divides, and the guard costs it nothing.
    print(p)
    print(p / 2.0)
    print("done")
    while True:
        pass


main()
