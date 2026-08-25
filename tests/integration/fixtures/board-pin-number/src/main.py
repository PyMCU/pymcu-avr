# PyMCU -- board-pin-number: Pin() takes the number on the silkscreen (PyMCU#50)
#
# 13 is the first number anyone writes on an Uno, and the AVR HAL used to take only a
# port name -- so Pin(13, Pin.OUT) was a compile error and the user had to know that
# Arduino 13 is PB5. Both spellings now go through the same compile-time match.
#
# The four pins cover all three ports and both halves of the numbering:
#   13 -> PB5 (built-in LED)   15 -> PC1 (A1)   7 -> PD7   2 -> PD2 (INT0, pull-up)
#
# The levels come from GPIOR0, which the test writes before the run: a fixture of
# literals would measure the constant folder rather than the pin mapping.
#
# Checkpoint via BREAK: directions configured and the three outputs driven; GPIOR1
# carries the complement of the seed, so a run that never read GPIOR0 is visible.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.hal.gpio import Pin
from pymcu.types import asm, uint8


def main() -> None:
    led = Pin(13, Pin.OUT)
    a1 = Pin(15, Pin.OUT)
    d7 = Pin(7, Pin.OUT)
    btn = Pin(2, Pin.IN_PULLUP)

    seed: uint8 = GPIOR0.value
    led.value(seed & 1)
    a1.value((seed >> 1) & 1)
    d7.value((seed >> 2) & 1)
    GPIOR1.value = seed ^ 0xFF

    asm("BREAK")

    while True:
        pass


main()
