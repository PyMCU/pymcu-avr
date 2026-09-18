# typing-extensions: PyMCU/PyMCU#462.
#
# `from typing_extensions import Protocol` is a no-op, the same way
# `from typing import Protocol` already is (#444). typing_extensions is resolved
# by the type system, never fetched as a module. CircuitPython libraries write
# this unguarded for Python 3.7 -- adafruit_register opens with it to declare
# I2CDeviceDriver(Protocol).
#
# Before this, DependencyGraphBuilder tried to LOAD typing_extensions like any
# third-party module and refused with "Module not found: typing_extensions", so
# this program did not build at all.
#
# The Protocol base is the structural-typing spelling the libraries use; Dev is
# a real class whose method is what actually runs. The discriminating value is
# 7, the field Dev stores. A wrong-but-building reading that skipped the method
# would never print 7.
#
# Expected UART output:
#   7
#   END
from typing_extensions import Protocol
from pymcu.types import uint8


class Driver(Protocol):
    def n(self) -> uint8: ...


class Dev:
    def __init__(self) -> None:
        self._n: uint8 = 7

    def n(self) -> uint8:
        return self._n


def main() -> None:
    d = Dev()
    print(d.n())
    print("END")
    while True:
        pass
