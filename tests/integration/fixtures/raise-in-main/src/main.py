# An unhandled raise written in the entry function halts with its name (PyMCU#339).
#
# It used to lower to the propagate-to-caller form, `SET; RET` -- and main has no caller. The
# stack pointer is at the top of SRAM, so the RET popped a return address that was never
# pushed and execution went wherever those bytes point: a stack underflow, and nothing on the
# UART, where the documented behaviour is `E:<TypeName>` and a halt.
#
# The test seeds GPIOR0 before running. With 90 the raise fires and the board must print
# E:ValueError and stop; with anything else it prints ALIVE and spins.
from pymcu.hal.uart import UART
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8


def main():
    uart = UART(9600)
    uart.println("GO")
    code: uint8 = GPIOR0.value
    if code == 90:
        raise ValueError
    uart.println("ALIVE")
    while True:
        pass
