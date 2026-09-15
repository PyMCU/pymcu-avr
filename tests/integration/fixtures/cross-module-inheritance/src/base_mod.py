# Half of the PyMCU/PyMCU#420 reproduction: the base, in its own file.
from pymcu.types import uint8


class Base:
    def __init__(self, a: uint8, b: uint8) -> None:
        self._a: uint8 = a
        self._b: uint8 = b

    def total(self) -> uint8:
        return self._a + self._b
