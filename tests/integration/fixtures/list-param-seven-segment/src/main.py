# PyMCU -- list-param-seven-segment: the acceptance program for a driver that takes a list
# of DigitalInOut (PyMCU#313) and reads a lookup table written as a plain list (PyMCU#317).
#
# This is the CircuitPython idiom: adafruit_ht16k33, adafruit_74hc595 and every 7-segment
# guide build the pin list first and hand it to the driver, and the digit patterns are a
# module-level list of ints with no annotation.
#
# Segments a..g on D2..D8, so a..f are PD2..PD7 and g is PB0. Each digit drives:
#
#   digit    0 1 2 3 4 5 6 7 8 9
#   a  D2    1 0 1 1 0 1 1 1 1 1
#   b  D3    1 1 1 1 1 0 0 1 1 1
#   c  D4    1 1 0 1 1 1 1 1 1 1
#   d  D5    1 0 1 1 0 1 1 0 1 1
#   e  D6    1 0 1 0 0 0 1 0 1 0
#   f  D7    1 0 0 0 1 1 1 0 1 1
#   g  D8    0 0 1 1 1 1 1 0 1 1
#
# Instead of a second per digit, each digit prints the two port registers, which is the
# same measurement the scope makes and the only one an emulator can check: reading the pin
# back would fold to the value just written and pass on a firmware that drives nothing.
#
# Expected UART: PORTD & 0xFC and PORTB & 0x01 per digit, then 0 0 after clear().
#   252 0 / 24 0 / 108 1 / 60 1 / 152 1 / 180 1 / 244 1 / 28 0 / 252 1 / 188 1 / 0 0
#   END
import board
import digitalio
from pymcu.chips.atmega328p import PORTB, PORTD
from pymcu.hal.console import print

DIGITS = [0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F]   # gfedcba


class SevenSegment:
    def __init__(self, segments):
        self._segments = segments
        for s in self._segments:
            s.direction = digitalio.Direction.OUTPUT

    def show(self, digit):
        pattern = DIGITS[digit]
        for i in range(7):
            self._segments[i].value = (pattern >> i) & 1

    def clear(self):
        for s in self._segments:
            s.value = False


pins = [digitalio.DigitalInOut(board.D2), digitalio.DigitalInOut(board.D3),
        digitalio.DigitalInOut(board.D4), digitalio.DigitalInOut(board.D5),
        digitalio.DigitalInOut(board.D6), digitalio.DigitalInOut(board.D7),
        digitalio.DigitalInOut(board.D8)]
display = SevenSegment(pins)


def main():
    for d in range(10):
        display.show(d)
        print(PORTD.value & 0xFC, PORTB.value & 0x01)
    display.clear()
    print(PORTD.value & 0xFC, PORTB.value & 0x01)
    print("END")


main()
