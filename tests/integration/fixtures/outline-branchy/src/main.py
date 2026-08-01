# Outlining of an @inline expansion that carries its own control flow.
#
# UART.write_hex() force-inlines two if/else branches plus, inside each
# uart_write(), a "wait until UDRE0 is set" polling loop. Every one of those
# branches and loops is internal to the expansion -- nothing jumps in from the
# outside and nothing jumps out -- so the repeated copies collapse into shared
# subroutines that keep the branches and the loop inside them.
#
# The bytes below cover both sides of every branch: 0x00 takes the low path
# twice, 0xFF the high path twice, 0x5A and 0xA5 one of each in both nibble
# positions. Printing them as hex proves the shared body still selects the
# right digit per nibble.
#
# UART output (9600 baud): "HX\n00FF5AA5"
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    uart.println("HX")

    uart.write_hex(0x00)
    uart.write_hex(0xFF)
    uart.write_hex(0x5A)
    uart.write_hex(0xA5)

    while True:
        pass
