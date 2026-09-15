# method-factory-returns-class: PyMCU/PyMCU#421.
#
# A method call that returns a class instance -- `led = pcf.get_pin(7)`, an ORDINARY method,
# not a constructor, whose body builds and returns a DigitalInOut -- never tagged the
# assignment target with a class, so the next method call on it (`led.switch_to_output(...)`)
# mangled to an undefined `led_switch_to_output`. Confirmed this is about the receiver's
# missing class, not the keyword argument: the same call fails identically written
# positionally.
#
# Reduced from adafruit_pcf8574.py's PCF8574.get_pin() / DigitalInOut.switch_to_output().
#
# Expected UART output: MF 1 END
from pymcu.types import uint8
from pymcu.hal.uart import UART


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


uart = UART(115200)
uart.println("MF")

o = Owner()
led = o.get_pin(7)
led.switch_to_output(value=True)
print(o.written)

uart.println("END")

while True:
    pass
