# PyMCU -- named-sequence-loop: declare the pins, then walk them.
#
# Regression for PyMCU#77. The shape every Arduino and MicroPython program starts with had
# no spelling: `pines = [11, 12, 13]` then `for p in pines:` compiled to a run-time loop,
# so `p` was not a constant and Pin(p) rejected it -- while the SAME literal written inline
# at the `for` unrolled and compiled. A named tuple did not get that far at all ("tuples
# are not supported as runtime values").
#
# Both bindings now unroll, so both drive real pins. On the Uno pinout:
#   listed = [8, 9]      -> PORTB (0x25) bits 0 and 1
#   tupled = (5, 6, 7)   -> PORTD (0x2B) bits 5, 6 and 7
#   enumerate over the list sets GPIOR0 to the last index seen (1)
from machine import Pin
from pymcu.types import asm, uint8
from pymcu.chips.atmega328p import GPIOR0

listed = [8, 9]
tupled = (5, 6, 7)


def main():
    for n in listed:
        Pin(n, Pin.OUT).value(1)

    for n in tupled:
        Pin(n, Pin.OUT).value(1)

    seen: uint8 = 0
    for i, n in enumerate(listed):
        seen = i
    GPIOR0.value = seen

    asm("BREAK")
    while True:
        pass
