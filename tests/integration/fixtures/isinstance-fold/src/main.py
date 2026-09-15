# isinstance-fold: PyMCU/PyMCU#424.
#
# `isinstance(x, T)` on a ZCA instance is not a decision, it is a fold: every instance has a
# class fixed when the program is compiled, so the answer is already known. Reduced from
# adafruit_mcp3xxx's own AnalogIn.__init__ (adafruit_mcp3xxx/analog_in.py):
#   from .mcp3xxx import MCP3xxx
#   if not isinstance(mcp, MCP3xxx): raise ValueError(...)
# called with an MCP3008 (a subclass of MCP3xxx, defined in a third file). PyMCU used to
# refuse isinstance() outright as a Python builtin it does not provide, so this program did
# not build at all.
#
# Expected UART output: IF 3 END
from base_mod import MCP3xxx
import sub_mod
from pymcu.types import uint8
from pymcu.hal.uart import UART


class AnalogIn:
    def __init__(self, mcp: MCP3xxx, pin: uint8) -> None:
        if not isinstance(mcp, MCP3xxx):
            raise ValueError("expected an MCP3xxx instance")
        self._mcp: MCP3xxx = mcp
        self._pin: uint8 = pin

    def pin_number(self) -> uint8:
        return self._pin


uart = UART(115200)
uart.println("IF")

m = sub_mod.MCP3008(8)
a = AnalogIn(m, 3)
print(a.pin_number())

uart.println("END")

while True:
    pass
