# cross-module-inheritance: PyMCU/PyMCU#420.
#
# A class inherited across a module boundary -- reduced from adafruit_mcp3xxx, where
# MCP3008(MCP3xxx) declares no __init__ of its own and inherits one from MCP3xxx, defined in
# a different file. PyMCU treated it as though it had no constructor of any kind: #391's
# synthesized no-op then gave it a real but wrong signature (zero parameters), so
# MCP3008(spi, cs) was refused for passing "too many arguments" to a constructor the library
# never wrote as taking none.
#
# Expected UART output: XM 10 END
import sub_mod
from pymcu.types import uint8
from pymcu.hal.uart import UART

uart = UART(115200)
uart.println("XM")

s = sub_mod.Sub(4, 6)
print(s.total())

uart.println("END")

while True:
    pass
