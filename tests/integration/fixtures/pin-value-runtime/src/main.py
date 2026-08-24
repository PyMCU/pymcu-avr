# PyMCU -- pin-value-runtime: drive a pin from a value computed at run time.
#
# Regression for PyMCU#57. `Pin.value(x)` declared x as `const`, so only a literal
# got through and `led.value(state)` -- the canonical Arduino line -- was rejected
# with "Parameter 'x' is declared as const and requires a compile-time constant".
#
# The value comes from GPIOR0, written by the test before the run, so nothing folds:
#   GPIOR0 (0x3E) = the value to drive (1 or 0)
#   PORTB  (0x25) bit 5 = what reached the pin
#   GPIOR1 (0x4A) = the same pin read back through value()
#
from pymcu.hal.gpio import Pin
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1


def main():
    led = Pin("PB5", Pin.OUT)
    v: uint8 = GPIOR0.value
    led.value(v)
    GPIOR1.value = led.value()
    asm("BREAK")
    while True:
        pass
