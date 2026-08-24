# PyMCU -- literal-width-module: the same widths, at module level.
#
# Regression for PyMCU#76. An unannotated module-level integer kept the width of its
# FIRST assignment and truncated every later value into it, silently: `b = 5` then
# `b = 300` printed 44 on the board, `c = -5` printed 251, `d = 70000` printed 112.
#
# Module level is the MicroPython and CircuitPython shape (no def main() at all), so
# this was the default spelling for anyone arriving from either port.
#
# Expected UART output: 200 / 300 / -5 / 70000 / 300 / done
from pymcu.hal.uart import UART

uart = UART(9600)

a = 200
uart.print_uint16(a)

b = 5
b = 300
uart.print_uint16(b)

c = -5
uart.print_int16(c)

d = 70000
uart.print_uint32(d)

e = 200
e = e + 100
uart.print_uint16(e)

uart.println("done")
