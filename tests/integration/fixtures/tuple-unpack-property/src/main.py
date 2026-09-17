# PyMCU -- tuple-unpack-property: `a, b = obj.prop` where the getter returns a
# tuple literal, in the shape adafruit_tcs34725 uses three levels deep.
#
# `x, y = f()` was supported for calls; the same unpack against a property
# (`r, g, b, clear = self.color_raw`) refused with "RHS must be a tuple literal
# or an inline function call", because a MemberAccessExpr never reached the
# lastTupleResults path. The getter is expanded inline and its returned tuple
# binds one result slot per element -- the same machinery, one more entry
# shape.
#
# Covered shapes:
#   a, b = s.pair            property returning a 2-tuple
#   r, g, b, c = s.raw       property whose getter unpacks a METHOD call
#                            returning a 4-tuple, then returns a reordered
#                            tuple literal (the color_raw shape)
#   x, y = s.method(...)     plain (non-@inline) method returning a tuple
#
# Expected UART output:
#   3
#   4
#   20
#   30
#   40
#   10
#   6
#   16
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Sensor:
    def __init__(self):
        self._v = 0

    def read4(self, base: int):
        return base + 10, base + 20, base + 30, base + 40

    @property
    def pair(self):
        return 3, 4

    @property
    def raw(self):
        c, r, g, b = self.read4(0)
        return r, g, b, c


uart = UART(9600)
s = Sensor()

a, b = s.pair
print(a)
print(b)

r, g, b, c = s.raw
print(r)
print(g)
print(b)
print(c)

w, x, y, z = s.read4(-4)
print(w)
print(x)

print("done")

while True:
    pass
