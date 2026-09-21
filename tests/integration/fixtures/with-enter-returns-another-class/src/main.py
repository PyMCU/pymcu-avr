# with-enter-returns-another-class: PyMCU/PyMCU#454.
#
# A with-block's context manager can hand back an instance of a DIFFERENT class than its own
# -- adafruit_bus_device's SPIDevice.__enter__ returns self.spi (a busio.SPI), not the
# SPIDevice itself. Reduced across two files, both a module-level `with` and one inside a
# method, matching adafruit_mcp3xxx's own `with self._spi_device as spi:
# spi.write_readinto(...)` shape. Refused before this fix as `call to undefined function
# 'inner_value'` / '..._spi_write_readinto'.
#
# Expected UART output: WE 11 13 END
import manager_mod
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Owner:
    def __init__(self, v: uint8) -> None:
        self._mgr: manager_mod.Manager = manager_mod.Manager(v)

    def read(self) -> uint8:
        with self._mgr as inner:
            return inner.value()


uart = UART(115200)
uart.println("WE")

mgr = manager_mod.Manager(11)
with mgr as inner:
    print(inner.value())

o = Owner(13)
print(o.read())

uart.println("END")

while True:
    pass
