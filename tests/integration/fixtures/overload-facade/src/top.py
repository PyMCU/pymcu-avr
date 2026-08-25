# The consumer. It imports through the facade UNDER AN ALIAS and calls the constructor
# from inside its own class, which is exactly what pymcu_micropython.machine does with
# `from pymcu.hal.gpio import Pin as _Pin`.
from pymcu.types import uint8, inline, const
from mid import Low as _Low


class Wrapper:
    @inline
    def __init__(self, k: uint8):
        # The port name is held in a FIELD and passed from there. Passing a literal or a
        # local both selected the right overload already; a field did not.
        self._name = "PB5"
        self._low = _Low(self._name, k)
        self.tag: uint8 = self._low.tag
