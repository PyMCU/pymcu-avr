# default-constructor: PyMCU/PyMCU#391.
#
# A class that declares no __init__ used to be refused at its first construction, though
# CPython synthesizes a trivial no-op constructor for such a class and runs it. Reduced from
# the issue's two reproducers (tests/oracle/probes/037_raise_method_caller.py and
# tests/oracle/probes/079_class_attribute_instance_read.py in PyMCU/PyMCU): a class with only
# methods and no state, and a class-level constant read back through an instance.
#
# Expected UART output: DC 42 5 6 END
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Sensor:
    def read(self, raw: uint8) -> uint8:
        if raw > 5:
            raise ValueError("range")
        return raw


class Device:
    SCALE = 5

    def read(self) -> uint8:
        return Device.SCALE + 1


uart = UART(115200)
uart.println("DC")

s = Sensor()
try:
    print(s.read(9))
except ValueError:
    print(42)

d = Device()
print(d.SCALE)
print(d.read())

uart.println("END")

while True:
    pass
