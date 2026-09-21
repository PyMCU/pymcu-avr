# striplib: the striplib.py half of cond-tuple-return. wheel() and Strip live in a
# module different from the caller's, matching adafruit_neopixel.py -- and ORDER is
# a module-level string constant, so `ORDER in {RGB, GRB}` asks a compile-time
# question about a binding spelled through the module.
from pymcu.types import uint8

RGB = "RGB"
GRB = "GRB"
ORDER = GRB


def wheel(pos: uint8):
    r = pos
    return (r, 2, 3) if ORDER in {RGB, GRB} else (r, 2, 3, 0)


class Strip:
    def __init__(self, n: uint8) -> None:
        self._n: uint8 = n
        self._buf = [0, 0, 0]

    def __setitem__(self, index: uint8, val) -> None:
        r, g, b = val
        self._buf[0] = r
        self._buf[1] = g
        self._buf[2] = b

    def at(self, i: uint8) -> uint8:
        return self._buf[i]
