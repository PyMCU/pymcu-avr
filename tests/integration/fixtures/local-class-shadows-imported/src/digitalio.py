# 1-argument DigitalInOut, the shape the CircuitPython module exports.
from pymcu.types import uint8


class DigitalInOut:
    def __init__(self, pin: uint8):
        self.pin = pin
