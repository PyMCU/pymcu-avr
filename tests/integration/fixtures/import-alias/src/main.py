# PyMCU -- import-alias: `import ... as` over a builtin the compiler lowers itself.
#
# Regression for PyMCU#66. `print` is not a symbol in pymcu.hal.console, it is a
# builtin, so an alias for it used to be mangled into `pymcu_hal_console_print` --
# a function nothing emits -- and the call failed with "undefined function", naming
# the mangled symbol instead of the alias the program wrote.
#
# Both spellings of the alias must reach the builtin. Expected UART output:
#   11
#   22
#   done
from pymcu.hal.console import print as p
import pymcu.hal.console as console
from pymcu.hal.uart import UART
from pymcu.types import uint8

uart = UART(9600)

a: uint8 = 11
p(a)

b: uint8 = 22
console.print(b)

uart.println("done")
