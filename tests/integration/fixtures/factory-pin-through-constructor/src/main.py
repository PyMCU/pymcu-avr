# factory-pin-through-constructor: adafruit_character_lcd's
#   Character_LCD.__init__(reset_dio: DigitalInOut, ...)
#   super().__init__(self.mcp.get_pin(1), ...)
# The factory's class owns the pin inside the constructor, not
# the annotation's digitalio.DigitalInOut.
#
# WHAT DISCRIMINATES:
#   7 -- MCP DigitalInOut.high() writes GPIOR0
#   99 would mean the annotation class won
from pymcu.chips.atmega328p import GPIOR0
from pymcu.time import delay_ms
from pymcu.types import uint8
from digitalio import DigitalInOut
from mcp import MCP


class Lcd:
    def __init__(self, a: DigitalInOut, b: DigitalInOut, columns: uint8, lines: uint8):
        self.columns = columns
        self.lines = lines
        self.reset = a
        self.enable = b
        self._n = 0
        self._m = 0
        for pin in (a, b):
            pin.high()


def main():
    while True:
        m = MCP()
        Lcd(m.get_pin(1), m.get_pin(2), 16, 2)
        print(GPIOR0.value)
        print("END")
        delay_ms(1200)
