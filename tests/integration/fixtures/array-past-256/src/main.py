# PyMCU -- array-past-256: an array larger than 256 bytes addresses all of it.
#
# Regression for pymcu-avr#11. A run-time array index was loaded as a SINGLE BYTE and the
# carry into Z's high byte was added against R1, which is zero, so the high half of the index
# never arrived: every element past the first 256 bytes aliased back into them. A uint16 array
# wrapped at index 128, because doubling the 8-bit index overflowed as well.
#
# Clean build, no diagnostic, and the program reads back plausible values from the wrong
# slots. What it cost: the SSD1306 driver keeps a uint8[1024] framebuffer, so show() put the
# first 256 bytes on the I2C bus four times and clear() cleared a quarter.
#
# The indices here are chosen to straddle the boundary: 255 is the last byte the old path
# could reach, 256 is the first it could not, and 300 and 511 are well past it. The uint16
# array checks 127 and 128, the wrap the doubling caused.
#
# Expected UART output:
#   11 22 33 44
#   55 66
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16

BUF: uint8[512] = bytearray(512)
W: uint16[200] = [0] * 200


def main():
    seed: uint16 = GPIOR0.value

    BUF[seed + 255] = 11
    BUF[seed + 256] = 22
    BUF[seed + 300] = 33
    BUF[seed + 511] = 44
    print(BUF[seed + 255], BUF[seed + 256], BUF[seed + 300], BUF[seed + 511])

    W[seed + 127] = 55
    W[seed + 128] = 66
    print(W[seed + 127], W[seed + 128])

    print("done")
