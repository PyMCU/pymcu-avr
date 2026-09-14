# CircuitPython bitbangio.I2C: a software bus on two pins the peripheral is not wired to
# (pymcu-circuitpython#10).
#
# The module was absent, and the soft I2C it wraps had been in the HAL all along. It is what
# a board with two sensors at the same address needs: an ATmega has one TWI.
#
# The test watches D2 (SCL) and D3 (SDA) between the two BREAKs and decodes the transfer out
# of the waveform: both lines idle high, a START, SLA+W for 0x68 (which is 0xD0), the payload
# 0xA5, and a STOP.
import board
import bitbangio
from pymcu.types import asm


def main():
    i2c = bitbangio.I2C(board.D2, board.D3, frequency=100000)
    asm("BREAK")
    i2c.writeto(0x68, b"\xA5")
    asm("BREAK")

    while True:
        pass
