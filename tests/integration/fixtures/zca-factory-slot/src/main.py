# RFC 0001 Model B (sret) -- a non-@inline factory returning a MULTI-field ZCA. A single field
# fits in the return register (register handle); two fields don't, so the factory uses sret:
# the CALLER allocates the instance's SRAM slot and passes its address as a hidden __self
# pointer; the factory stores each field through it and returns the pointer. The instance's
# (default-outlined) method then reads fields from that slot via self-ptr.
#
#   s = make(3, 4)  -> slot {pin:3, gain:4}; s.read() = 3*4 = 12
#   t = make(5, 7)  -> distinct slot {pin:5, gain:7}; t.read() = 5*7 = 35
#
# Two factory calls => two distinct slots (no aliasing, because the caller owns each slot).
# UART output: "FS\n" banner, then 12, then 35.
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self, pin: uint8, gain: uint8):
        self.pin = pin
        self.gain = gain

    def read(self) -> uint8:
        return self.pin * self.gain


def make(pin: uint8, gain: uint8) -> Sensor:
    return Sensor(pin, gain)


def main():
    uart = UART(9600)
    uart.println("FS")

    s = make(3, 4)
    t = make(5, 7)

    uart.write(s.read())   # 12
    uart.write(t.read())   # 35

    while True:
        pass
