# MicroPython: an I2C address that NACKs must raise OSError [Errno 5] EIO, the
# way the rp2 port reports it, so a try/except OSError around the call actually
# runs. It used to return silently, so a dead bus looked exactly like a live
# one.
#
# With a recorder NACKing every address the UART shows:
#   nack [Errno 5] EIO
# With one ACKing 0x3C the writeto succeeds and nothing prints.

from machine import I2C


def main():
    i2c = I2C()
    try:
        i2c.writeto(0x3C, 0x00)
    except OSError as e:
        print("nack", e)

    while True:
        pass
