# descriptor-obj-type: PyMCU/PyMCU#419.
#
# Descriptor protocol __get__/__set__ receive obj as the owning instance.
# Adafruit annotates that parameter with a typing-only placeholder
# (I2CDeviceDriver); CPython still hands over the real device. The rewrite
# must substitute the argument's class so obj.base resolves.
#
# WHAT DISCRIMINATES: 11 (9 + 2 through __get__) and 5 (obj.base after __set__).
# A compile that still treated obj as I2CDeviceDriver would not build.
from pymcu.types import uint8
from pymcu.time import delay_ms

if TYPE_CHECKING:
    from circuitpython_typing.device_drivers import I2CDeviceDriver


class Field:
    def __init__(self, addr: uint8) -> None:
        self.addr = addr

    def __get__(self, obj: I2CDeviceDriver, objtype=None) -> uint8:
        return self.addr + obj.base

    def __set__(self, obj: I2CDeviceDriver, value: uint8) -> None:
        obj.base = value


class Dev:
    reg = Field(9)

    def __init__(self, base: uint8) -> None:
        self.base = base


d = Dev(2)


def main():
    while True:
        print(d.reg)
        d.reg = 5
        print(d.base)
        print("END")
        delay_ms(1200)
