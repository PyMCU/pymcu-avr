# Adafruit TYPE_CHECKING guard (motor.py / servo.py): an inner
# except NotImplementedError around from pwmio import PWMOut.
# PyMCU#480: that except used to drop PWMOut. #481: the stub handler
# used to load even when pwmio was there (circuitpython_typing.pwmio
# is not in this fixture -- a build that still resolved the handler
# would fail Module not found).
from pymcu.types import uint8

try:
    from typing import Optional, Type
    try:
        from pwmio import PWMOut
    except NotImplementedError:
        from circuitpython_typing.pwmio import PWMOut
except ImportError:
    pass


class Thing:
    def __init__(self, h: PWMOut) -> None:
        self._h = h

    def val(self) -> uint8:
        return self._h.n
