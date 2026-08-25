# The class a user would naturally put in its own file.
from pymcu.types import uint16


class Sensor:
    def __init__(self, base: uint16):
        self.base: uint16 = base

    def read(self, raw: uint16) -> uint16:
        return self.base + raw


def make(base: uint16) -> uint16:
    # Constructing the class INSIDE its own module failed too, which is what showed the bug
    # was not about crossing the import boundary at the call site.
    s = Sensor(base)
    return s.read(1)
