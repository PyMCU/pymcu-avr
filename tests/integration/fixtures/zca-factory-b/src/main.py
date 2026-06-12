# RFC 0001 Model B (register-packed handle) -- the factory bug, fixed without forcing
# @inline. A single-field ZCA has no runtime struct, so a non-@inline factory returns the
# instance's packed field as a scalar "handle" in the return register. The use site tracks
# `s = make_sensor(...)` as a handle instance, and s.read() (an @outline shared method)
# receives that scalar as its self field.
#
#   make_sensor(20) returns the packed pin = 20 + 1 = 21
#   s.read() = self.pin * 2 = 21 * 2 = 42 = '*'
#   make_sensor(40) returns 41; t.read() = 82 = 'R'
#
# UART output: "FB\n" banner, then '*' (42), then 'R' (82)  ->  contains "*R".
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self, pin: uint8):
        self.pin = pin

    def read(self) -> uint8:
        return self.pin * 2


# Non-@inline factory: returns a ZCA across a real call boundary. Before Model B this
# failed at link with `undefined reference to <var>_read`.
def make_sensor(base: uint8) -> Sensor:
    return Sensor(base + 1)


def main():
    uart = UART(9600)
    uart.println("FB")

    s = make_sensor(20)   # handle = 21
    t = make_sensor(40)   # handle = 41

    uart.write(s.read())  # 42 = '*'
    uart.write(t.read())  # 82 = 'R'

    while True:
        pass
