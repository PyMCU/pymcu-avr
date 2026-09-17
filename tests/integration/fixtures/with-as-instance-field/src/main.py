# PyMCU -- with-as-instance-field: __enter__ returns an instance of ANOTHER class
# (PyMCU#454).
#
# `with self._spi_device as spi:` is how adafruit_bus_device's SPIDevice works --
# __enter__ acquires the bus and returns self.spi, an SPI instance. The bound name
# used to keep the manager alias or degrade to a scalar copy of a flattened field,
# so `spi.write_readinto(...)` resolved to a free function nothing emitted
# ("call to undefined function 'spi_write_readinto'"). The bound name must take the
# returned instance's storage AND its class.
#
# Two shapes are pinned: `with m as i` at module level, and `with self._mgr as x`
# inside a method -- the mcp3xxx shape, where the as-name is a method local and the
# manager a field of the enclosing instance.
#
# Expected UART output:
#   7
#   41
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Inner:
    def __init__(self, v: int) -> None:
        self.v: int = v

    def read(self) -> int:
        return self.v

    def bump(self, k: int) -> int:
        return self.v + k


class Manager:
    def __init__(self, v: int) -> None:
        self.inner = Inner(v)

    def __enter__(self) -> Inner:
        return self.inner

    def __exit__(self) -> None:
        pass


class Device:
    def __init__(self, v: int) -> None:
        self._mgr = Manager(v)

    def read_bus(self, k: int) -> int:
        with self._mgr as bus:
            return bus.bump(k)


uart = UART(9600)

m = Manager(7)
with m as i:
    print(i.read())

d = Device(39)
print(d.read_bus(2))

print("done")

while True:
    pass
