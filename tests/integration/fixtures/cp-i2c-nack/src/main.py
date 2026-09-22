# CircuitPython: an I2C address that NACKs must raise OSError, the way the
# RP2040 port does ([Errno 19] No such device), so a try/except OSError around
# the call actually runs. That is the field report this fixture pins down: an
# Uno and a dark SSD1306 whose "no ack" handler never ran because writeto
# ignored the TWI status.
#
# With a recorder NACKing every address the UART shows:
#   nack [Errno 19] No such device
# With one ACKing 0x3C the writeto succeeds and nothing prints.

import board
import busio


def main():
    i2c = busio.I2C(board.SCL, board.SDA)
    try:
        i2c.writeto(0x3C, b"\x00")
    except OSError as e:
        print("nack", e)

    while True:
        pass
