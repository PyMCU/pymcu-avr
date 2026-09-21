# compat-cp-annotated-i2c-param: a method call on a parameter annotated with an
# imported class.
#
# `def probe(b: busio.I2C): b.try_lock()` is the shape the SSD1306 bisect hit:
# the instance comes from board.I2C() -- a plain function that returns the busio
# class, not the constructor itself -- and the call on the annotated parameter
# flattened the receiver's own name into the undefined 'i2c_try_lock' instead of
# dispatching to busio.I2C.try_lock.
#
# On the wire, try_lock() emits nothing and writeto(0x3C, b"") is exactly a
# START, the SLA+W byte for 0x3C, and a STOP -- the empty write the test waits
# for before "OK" leaves the UART.
import board
import busio
from pymcu.hal.console import print


def probe(b: busio.I2C):
    b.try_lock()
    b.writeto(0x3C, b"")


def main():
    i2c = board.I2C()
    probe(i2c)
    print("OK")
