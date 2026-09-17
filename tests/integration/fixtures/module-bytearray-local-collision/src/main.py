# PyMCU -- module-bytearray-local-collision: a module container keeps its own type,
# and a `global` write still reaches it.
#
# Regression for PyMCU#458. A module-level `bytearray` whose name a linked stdlib
# function uses for one of its own locals leaked INTO that function's checks: the
# "is this a container" question asked about `data`/`b` found the user's global
# array instead of the function's local scalar, and refused the build inside
# stdlib code with a limitation the stdlib does not have:
#
#   uart_rx_read         -> `return data` -- "a bytes or list object cannot be returned"
#   uart_write_byte_repr -> `if b == 92`  -- "a bytes or list object cannot be compared"
#
# Both functions link whenever the UART module is in the program, which `print`
# alone arranges, so a global named `data` or `b` -- the most common buffer names
# in CircuitPython-style code -- refused almost any program. The inverse
# collision (a function local overwriting a module array's size) was #167; this
# fixture is its mirror, and also pins that a local of the same name still
# shadows the global array in the function's own body.
#
# The names are the measurement -- do not "tidy" them.
#
# (A `global data` + `data[i] = v` store from inside a function is a different,
# older bug -- the module array is split between `main.data` and `data` -- and is
# tracked separately rather than exercised here.)
#
# Expected UART output:
#   7
#   42
#   4
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART

data = bytearray(2)
b = bytearray(3)

data[0] = 7
b[0] = 42

uart = UART(9600)


def shadowed():
    # A local scalar of the same name: reads and writes here are the local's,
    # and the module array is untouched.
    data: int = 3
    data = data + 1
    print(data)


print(data[0])
print(b[0])
shadowed()
print("done")

while True:
    pass
