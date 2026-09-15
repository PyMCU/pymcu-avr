# Half of the PyMCU/PyMCU#421 reproduction: Pin and Owner, in their own file -- matching
# adafruit_pcf8574.py, where PCF8574.get_pin() and its DigitalInOut return type are both
# defined in a module different from the one that calls get_pin().
from pymcu.types import uint8


class Pin:
    def __init__(self, n: uint8, owner: "Owner") -> None:
        self._n: uint8 = n
        self._owner: Owner = owner

    def switch_to_output(self, value: bool = False, **kwargs) -> None:
        self._owner.written = value


class Owner:
    def __init__(self) -> None:
        self.written: bool = False

    def get_pin(self, n: uint8) -> Pin:
        return Pin(n, self)
