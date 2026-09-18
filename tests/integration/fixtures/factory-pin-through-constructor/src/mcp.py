# adafruit_mcp230xx: a local DigitalInOut next to import digitalio,
# returned by get_pin and passed into Character_LCD.__init__.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
import digitalio


class DigitalInOut:
    def __init__(self, pin: uint8, parent: "MCP"):
        self.pin = pin
        self.parent = parent
        self._a = 0
        self._b = 0

    def high(self):
        GPIOR0.value = 7


class MCP:
    def __init__(self):
        self.n = 1
        self._a = 0
        self._b = 0
        self._c = 0
        self._d = 0

    def get_pin(self, pin: uint8) -> DigitalInOut:
        return DigitalInOut(pin, self)
