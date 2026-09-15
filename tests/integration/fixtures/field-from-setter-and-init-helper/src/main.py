# field-from-setter-and-init-helper: PyMCU#397/#441 -- a field first assigned OUTSIDE
# __init__, from a property setter (adafruit_tcs34725's integration_time.setter shape)
# and from a plain method __init__ calls directly (adafruit_motor.servo's
# set_pulse_width_range shape). DeriveFieldLayout used to derive the whole field layout
# from __init__ alone, so both `self._min_duty` and `self._offset` here were invisible
# to the layout and the build failed with "'Sensor' has no field '...'".
#
# Measured against CPython 3.12, real MicroPython v1.21.0 and real CircuitPython 9.2.1
# (issue #441): raw=10, min_duty=11, offset=5 -- exactly what plain Python arithmetic
# gives, since a field set this way is exactly as real as one set in __init__.
#
# Expected UART output:
#   raw=10
#   min_duty=11
#   offset=5
from pymcu.types import uint8
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self, raw: uint8):
        self._raw: uint8 = raw
        # A plain method called directly from __init__ -- the adafruit_motor.servo shape.
        self.configure(raw)

    def configure(self, raw: uint8):
        self._min_duty: uint8 = raw + 1

    @property
    def offset(self) -> uint8:
        return self._offset

    # A property setter -- the adafruit_tcs34725 shape.
    @offset.setter
    def offset(self, val: uint8):
        self._offset: uint8 = val


def main():
    uart = UART(9600)
    s = Sensor(10)
    s.offset = 5

    uart.write_str("raw=")
    uart.print_byte(s._raw)
    uart.write_str("min_duty=")
    uart.print_byte(s._min_duty)
    uart.write_str("offset=")
    uart.print_byte(s.offset)

    while True:
        pass
