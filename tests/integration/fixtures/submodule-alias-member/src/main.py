# submodule-alias-member: PyMCU/PyMCU#422.
#
# `import pkg.submodule as alias` resolves the alias to the full DOTTED module path, and
# reading a member through it (`alias.NAME`) mangled the member's symbol keeping the literal
# dot ("pkg.sub_P0") instead of the underscored form the module's own scan prefix is
# registered under ("pkg_sub_P0").
#
# Reduced from adafruit_mcp3xxx: `import adafruit_mcp3xxx.mcp3008 as MCP` then `MCP.P0`, a
# plain module-level int constant defined in the submodule.
#
# Expected UART output: SA 0 1 END
import pkg.sub as MCP
from pymcu.hal.uart import UART

uart = UART(115200)
uart.println("SA")

print(MCP.P0)
print(MCP.P1)

uart.println("END")

while True:
    pass
