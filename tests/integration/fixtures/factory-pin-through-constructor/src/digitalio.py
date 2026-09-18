# 1-argument DigitalInOut. high() stores 99 -- must not run.
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8


class DigitalInOut:
    def __init__(self, pin: uint8):
        self.pin = pin
        self._a = 0
        self._b = 0
        self._c = 0

    def high(self):
        GPIOR0.value = 99
