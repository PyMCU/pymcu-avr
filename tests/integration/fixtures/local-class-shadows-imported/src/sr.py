# adafruit_74hc595: a local DigitalInOut next to `import digitalio`,
# constructed as DigitalInOut(pin, self).
from pymcu.types import uint8
import digitalio


class DigitalInOut:
    def __init__(self, pin: uint8, parent: "ShiftRegister"):
        self.pin = pin
        self.parent = parent

    def get(self) -> uint8:
        return self.pin + 1


class ShiftRegister:
    def __init__(self):
        self.n = 1

    def get_pin(self, pin: uint8):
        return DigitalInOut(pin, self)
