# PyMCU -- in-pullup: Pin.IN_PULLUP, the name Arduino and MicroPython users write.
#
# Regression for PyMCU#59. The capability was always here as
# `Pin("PB0", Pin.IN, Pin.PULL_UP)`; only the combined constant was missing, and the
# error said the attribute did not exist, which is true and useless -- the user was
# not guessing, they were writing the name their previous platform uses.
#
# What must be true on the pin (checked by the test on the registers):
#   DDRB  (0x24) bit 0 = 0   input
#   PORTB (0x25) bit 0 = 1   pull-up enabled
from pymcu.hal.gpio import Pin
from pymcu.types import asm
from pymcu.chips.atmega328p import GPIOR0


def main():
    boton = Pin("PB0", Pin.IN_PULLUP)
    GPIOR0.value = boton.value()
    asm("BREAK")
    while True:
        pass
