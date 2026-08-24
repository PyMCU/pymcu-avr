# PyMCU -- instance-array-index: pins[i] with i varying at run time.
#
# Regression for PyMCU#68. A list of peripheral objects could not be indexed with a
# run-time value under any annotation, so the LED chaser, the keypad scan, the
# multiplexed display and the stepper sequence had no spelling: `for p in pins` covers
# "do the same to all of them", not "act on the i-th", which is the interesting half.
#
# The elements are separate compile-time instances, so the call is lowered as a
# selection over the constant indices. What the test checks is that the SELECTION picks
# the right one:
#   GPIOR0 (0x3E) = i, written by the test before the run
#   PORTB  (0x25) bit i = 1, and the other bit untouched
#   GPIOR1 (0x4A) = pins[i].value(), a value-returning method through the same path
from pymcu.hal.gpio import Pin
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1


def main():
    pins = [Pin("PB0", Pin.OUT), Pin("PB1", Pin.OUT)]
    i: uint8 = GPIOR0.value
    pins[i].high()
    GPIOR1.value = pins[i].value()
    asm("BREAK")
    while True:
        pass
