# outlined-self-call-override: PyMCU/PyMCU#373.
#
# A method call on self, from inside another method reached from __init__, used to report
# the receiver as an integer -- reduced from adafruit_bmp280.py and adafruit_veml7700.py:
# a base whose __init__ constructs >= 2 fields (so the class is compiled as a shared
# "outlined" body, self bound to the DECLARING class, not the constructed instance) calls
# self._reset(), which calls self._write_register_byte(...). Base's own
# _write_register_byte is `raise NotImplementedError()`, and a subclass overrides it.
#
# The forwarding an outlined method's self-call uses is static, to whatever the declaring
# class's own definition resolves to -- sound only when that target is ALSO a shared body.
# Base's own version is not (a raise is not outline-safe), so the forward used to fail and
# fall through to a generic per-instance receiver resolver with no instance to resolve
# against, reporting self as the plain integer its own storage happens to be.
#
# Expected UART output: OSC 182 END
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Base:
    def __init__(self) -> None:
        self._mode: uint8 = 0
        self._standby: uint8 = 1
        self._last: uint8 = 0
        self._reset()

    def _reset(self) -> None:
        self._write_register_byte(0xB6)

    def _write_register_byte(self, value: uint8) -> None:
        raise NotImplementedError()


class I2C(Base):
    def __init__(self) -> None:
        super().__init__()

    def _write_register_byte(self, value: uint8) -> None:
        self._last = value


uart = UART(115200)
uart.println("OSC")
d = I2C()
print(d._last)
uart.println("END")

while True:
    pass
