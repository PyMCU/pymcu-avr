# with-bound-name-field-read: PyMCU/PyMCU#390.
#
# `with obj as x:` bound x to a name qualified by the enclosing FUNCTION, which is not how
# the object it stands for is actually named: a module-level instance is a module global
# under its bare name, and a `with` inside a force-inlined method names its locals by the
# INLINE frame, not the function. Either mismatch left x reading storage nothing else ever
# wrote (fields silently 0), or -- for a method call on x, the shape
# adafruit_bmp280.py:463 stops on (`with self._i2c as i2c: i2c.write(...)`) -- left the
# receiver with no class at all.
#
# Reduced from tests/oracle/probes/029_with_context.py, 030_multi_with_context.py, and the
# bmp280 method-call shape.
#
# Expected UART output: WB 1 3 11 12 7 END
from pymcu.types import uint8
from pymcu.hal.uart import UART


class EnterSets:
    def __init__(self, v: uint8) -> None:
        self.v: uint8 = v

    def __enter__(self) -> "EnterSets":
        self.v = self.v + 1
        return self

    def __exit__(self, typ, val, tb) -> bool:
        return False


class Adder:
    def __init__(self, v: uint8) -> None:
        self.v: uint8 = v

    def __enter__(self) -> "Adder":
        return self

    def __exit__(self, typ, val, tb) -> bool:
        self.v = self.v + 10
        return False


class Bus:
    def __init__(self, addr: uint8) -> None:
        self.addr: uint8 = addr

    def __enter__(self) -> "Bus":
        return self

    def __exit__(self, typ, val, tb) -> bool:
        return False

    def write(self, value: uint8) -> uint8:
        return self.addr + value


class Device:
    def __init__(self, bus: Bus) -> None:
        self._bus: Bus = bus

    def poke(self, value: uint8) -> uint8:
        with self._bus as bus:
            return bus.write(value)


uart = UART(115200)
uart.println("WB")

# probe 029: the bound name reads what __enter__ just set (0 -> 1), not the pre-enter value.
g = EnterSets(0)
with g as h:
    print(h.v)

# probe 030: two managers in one statement, both fields set entirely in __init__ before the
# with block runs, __enter__ leaves them unchanged. Both bound names have to read the values
# __init__ set (1 + 2 = 3); after the block, __exit__ has added 10 to each.
a = Adder(1)
b = Adder(2)
with a as x, b as y:
    print(x.v + y.v)
print(a.v)
print(b.v)

# bmp280 shape: a method call on the bound name, inside a method that is itself
# force-inlined at its own call site.
bus = Bus(5)
d = Device(bus)
print(d.poke(2))

uart.println("END")

while True:
    pass
