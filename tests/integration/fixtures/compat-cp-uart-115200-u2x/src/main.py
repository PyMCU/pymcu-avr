# CircuitPython busio.UART(baudrate=115200) has to reach the same registers the native
# UART(115200) reaches: U2X0 double speed with UBRR=16, which is 115942 baud, +0.64%.
#
# fixtures/uart-115200-u2x pins that for pymcu.hal.uart. Nothing pinned it for the compat
# layer, and the compat layer is the one that carries the rate through a declared
# parameter: 115200 does not fit a uint16, so a layer that says uint16 hands the HAL
# 49664 and the part is configured for 50000 baud with no diagnostic. The emulator does
# not model a baud mismatch, so what that looks like from above is a receive test losing
# bytes, not a wrong rate. This fixture pins the registers where the rate is decided.
#
# Sends 'U' at 115200 after init.
import board
from busio import UART


def main():
    uart = UART(board.TX, board.RX, baudrate=115200)
    uart.write(b'U')
    while True:
        pass
