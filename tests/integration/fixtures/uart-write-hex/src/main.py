# uart-write-hex: each nibble prints its own digit, at every call site.
#
# write_hex is an @inline whose two halves add a different offset (48 for a
# digit, 55 for a letter). Several calls in a row, with values that exercise
# both halves and both nibble positions, catch a build that shares one call
# site's constant -- or one that loses the value on its way out of an outlined
# copy of the expansion.
#
# Expected UART output:
#   HEX
#   FF 3C 00 A5 0F
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("HEX")

    uart.write_hex(0xFF)
    uart.write(32)
    uart.write_hex(0x3C)
    uart.write(32)

    zero: uint8 = 0
    uart.write_hex(zero)
    uart.write(32)

    mixed: uint8 = 0xA5
    uart.write_hex(mixed)
    uart.write(32)

    low: uint8 = 0x0F
    uart.write_hex(low)
    uart.write(10)

    while True:
        pass
