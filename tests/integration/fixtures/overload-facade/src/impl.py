# The module that DEFINES the overloaded class. Two __init__ that differ only in the
# type of the first parameter, which is the shape a HAL uses to accept both a port
# name and a board pin number.
from pymcu.types import uint8, inline, const


class Low:
    @inline
    def __init__(self, s: const[str], k: uint8):
        self.tag: uint8 = 20 + k

    @inline
    def __init__(self, n: const[uint8], k: uint8):
        self.tag: uint8 = 10 + k
