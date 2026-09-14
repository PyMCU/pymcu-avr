# board.I2C(), board.SPI() and board.UART() (pymcu-circuitpython#7).
#
# `i2c = board.I2C()` is the first line of nearly every Adafruit sensor guide, and none of
# the three existed: every guide had to be edited to name the pins itself. They are
# functions rather than module-level objects so that a program that imports board and never
# asks for a bus programs no peripheral.
#
# At the BREAK, each bus has been built with the board's own pins and default settings:
#   GPIOR0 = TWBR    72    100 kHz at 16 MHz
#   GPIOR1 = SPCR    0x50  SPE|MSTR, mode 0 at fosc/4
#   GPIOR2 = UCSR0C  0x06  8N1
import board
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, TWBR, SPCR, UCSR0C
from pymcu.types import asm


def main():
    i2c = board.I2C()
    spi = board.SPI()
    uart = board.UART()

    GPIOR0.value = TWBR.value
    GPIOR1.value = SPCR.value
    GPIOR2.value = UCSR0C.value
    asm("BREAK")

    while True:
        pass
