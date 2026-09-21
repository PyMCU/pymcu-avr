# pinlib: the pinlib.py half of factory-pin-list-loop. Pin and Owner live in a
# module different from the caller's, matching adafruit_pcf8574.py, where
# PCF8574.get_pin() and its DigitalInOut return type are both defined in the
# library module.
from pymcu.hal.console import print
from pymcu.types import uint8


class Pin:
    def __init__(self, n: uint8, owner: "Owner") -> None:
        self._n: uint8 = n
        self._owner: Owner = owner

    def switch_to_output(self, value: bool = False, **kwargs) -> None:
        self._owner.count = self._owner.count + 1
        print(self._n)


class Owner:
    def __init__(self) -> None:
        self.count: uint8 = 0

    def get_pin(self, n: uint8) -> Pin:
        return Pin(n, self)
