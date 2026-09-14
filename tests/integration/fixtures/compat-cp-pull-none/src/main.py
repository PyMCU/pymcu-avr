# PyMCU -- compat-cp-pull-none: `pull = None` is "no pull" (PyMCU#306).
#
# None is how CircuitPython spells no pull, and the layer's setter dispatches on it with a
# `match`. None is not a constant -- it has no value to compare -- so the match stayed a
# run-time one, every arm was lowered, and the Pull.DOWN arm's compile-time refusal fired for
# a program that never asked for a pull-down: "Pull-down resistor not supported on AVR".
#
# D2 with no pull leaves PORTD bit 2 clear; D3 with Pull.UP sets PORTD bit 3. Both stay
# inputs, so DDRD keeps both bits clear.
import board
import digitalio
from pymcu.chips.atmega328p import DDRD, PORTD
from pymcu.hal.console import print


def main():
    free = digitalio.DigitalInOut(board.D2)
    free.direction = digitalio.Direction.INPUT
    free.pull = None

    up = digitalio.DigitalInOut(board.D3)
    up.direction = digitalio.Direction.INPUT
    up.pull = digitalio.Pull.UP

    print("DDR", DDRD.value & 0x0C)
    print("PORT", PORTD.value & 0x0C)
    print("END")
