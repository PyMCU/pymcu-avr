# PyMCU#429 -- a factory function returning a class instance whose SOLE field is a bare,
# UNANNOTATED constructor-parameter passthrough (`self.base = base`, no `self.base: uint8`).
#
# DeriveFieldLayout (Scan.cs) derived an empty type string for such a field -- IsOutlineSafe's
# scalar check reads "" as "not a plain value" and refuses to outline every method of the
# class, `read()` included. Its call site then fell back to the ordinary Model A flattened
# `<instance>_<field>` naming convention, a name this factory-handle assignment (RFC 0001
# Model B) never writes, so it read as zero: `s.read()` computed 0 + 1 = 1 instead of 10.
#
# Contrast with fixtures/zca-factory-b, whose single field IS annotated in __init__
# (`def __init__(self, pin: uint8)`), so it never took the empty-type path this fixture
# targets.
#
# Expected UART output (CPython prints 10, then 25):
#   10
#   25
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self, base):
        self.base = base

    def read(self):
        return self.base + 1


def make_sensor(base: int) -> Sensor:
    return Sensor(base)


uart = UART(9600)

s = make_sensor(9)
print(s.read())

t = make_sensor(24)
print(t.read())

while True:
    pass
