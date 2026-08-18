# Value-returning methods on a nested ZCA field, the shape the old diagnostic
# ("a ZCA field that is itself a ZCA, like self.pin.pulse_in()") declared
# unsupported. Three variants pin the current behaviour (force-inline / class
# recovery): a constructed nested field, a field bound from a parameter, and
# an undecorated (outline-eligible) method on a multi-field class.
#
# Expected UART output (9600 baud): "5" "43" "43" then 'D'.
from pymcu.hal.uart import UART
from pymcu.types import uint8, uint16, inline


class Inner:
    @inline
    def __init__(self, base: uint8):
        self._base = base

    @inline
    def read(self) -> uint8:
        return self._base + 1


class Outer:
    @inline
    def __init__(self):
        self._inner = Inner(4)

    @inline
    def get(self) -> uint8:
        return self._inner.read()


class Sensor:
    @inline
    def __init__(self, inner: Inner):
        self._inner = inner
        self._count = 0

    def sample(self) -> uint16:
        v: uint16 = self._inner.read()
        self._count = self._count + 1
        return v


def main():
    uart = UART(9600)
    o = Outer()
    uart.print_uint16(o.get())

    i = Inner(42)
    s = Sensor(i)
    uart.print_uint16(s.sample())
    uart.print_uint16(s.sample())
    uart.write('D')
