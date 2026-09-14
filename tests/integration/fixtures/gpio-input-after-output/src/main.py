# PyMCU -- gpio-input-after-output: an input has the pull the program asked for, and
# nothing else (PyMCU#309).
#
# On the AVR the pull-up and the output level are the same latch, PORTx. mode(IN) used to
# clear DDRx and leave PORTx alone, so a pin driven high and then made an input came back
# with the pull-up on: measured on an Arduino Uno, `high()` then `mode(IN)` left D6 at a
# firm 5 V where CircuitPython's `direction = INPUT` with pull None floats. mode(IN_PULLUP)
# wrote `3 ^ 1 = 2` into the one-bit direction slot, and mode(OPEN_DRAIN) a 3 (refused
# now, see tests/stdlib/test_gpio_input_latch.py).
#
# Read back through the CPU into the GPIORs at four breaks (GPIOR1 = DDRD, GPIOR2 = PORTD):
#   1  Pin("PD6", OUT), high(), mode(IN)            -> DDRD6 0, PORTD6 0  (floats)
#   2  pull(1), mode(OUT), low(), mode(IN)          -> DDRD6 0, PORTD6 1  (the pull-up asked for)
#   3  pull(0), mode(IN)                            -> DDRD6 0, PORTD6 0
#   4  mode(IN_PULLUP)                              -> DDRD6 0, PORTD6 1
from pymcu.chips.atmega328p import GPIOR1, GPIOR2, DDRD, PORTD
from pymcu.hal.gpio import Pin
from pymcu.types import asm


def main():
    p = Pin("PD6", Pin.OUT)
    p.high()
    p.mode(Pin.IN)
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    p.pull(1)
    p.mode(Pin.OUT)
    p.low()
    p.mode(Pin.IN)
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    p.pull(0)
    p.mode(Pin.IN)
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    p.mode(Pin.IN_PULLUP)
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    while True:
        pass
