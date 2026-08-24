# PyMCU -- bytes-literal: b"..." as a fixed buffer.
#
# Regression for PyMCU#55. `b: bytes = b"ab"` died as "IR Generation: Unknown Expression
# type: ListExpr" -- a compiler phase name and an internal AST class name, about the way
# protocol constants are written on an MCU: a register sequence for an I2C device, a
# command frame for a display, a CRC table.
#
# The literal carries its own size, and both spellings mean the same buffer:
#   GPIOR0 (0x3E) = frame[1]     from `bytes`      = 0x02
#   GPIOR1 (0x4A) = mutable[0]   from `bytearray`  = 0x41
#   GPIOR2 (0x4B) = mutable[1] after a store       = 0x7F
from pymcu.types import asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2


def main():
    frame: bytes = b"\x01\x02\x03"
    mutable: bytearray = b"AB"

    GPIOR0.value = frame[1]
    GPIOR1.value = mutable[0]

    mutable[1] = 0x7F
    GPIOR2.value = mutable[1]

    asm("BREAK")
    while True:
        pass
