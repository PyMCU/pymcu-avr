# PyMCU -- ffi-wide-args: an @extern parameter is as wide as it was DECLARED
#
# An @extern function has no body, so it never reaches the IR function list. When
# the backend sized each argument by the width of the VALUE instead of the width
# of the parameter, wide_sum3(1, 2, 3) loaded R24/R22/R20 and left R25/R23/R21
# holding whatever the previous call had left there.
#
# The first call passes large values on purpose, so those high halves are not
# zero when the second call runs.
#
# Expected UART output (9600 baud, 16 MHz):
#   "WIDE\n"
#   "B:4660\n"    -- wide_echo0(0x1234, 0x5678, 0x9ABC) = 0x1234 = 4660
#   "S:6\n"       -- wide_sum3(1, 2, 3) = 6
#   "V:600\n"     -- wide_sum3(n, n, n) with n a uint8 holding 200
#   "E:65538\n"   -- wide_echo32(0x00010002); swapped halves would read 131073
#   "T:65539\n"   -- wide_sum32(0x00010002, 1)
#   "F:6\n"       -- wide_scale_to_u16(1.5, 4.0) = 6
#   "G:7\n"       -- wide_scale_to_u16(3, 2.5) = 7, the integer promoted to float
#   "OK\n"

from pymcu.types import uint8, uint16, uint32, inline
from pymcu.hal.uart import UART
from pymcu.ffi import extern


@extern("wide_sum3")
def wide_sum3(a: uint16, b: uint16, c: uint16) -> uint16:
    pass

@extern("wide_echo0")
def wide_echo0(a: uint16, b: uint16, c: uint16) -> uint16:
    pass

@extern("wide_echo32")
def wide_echo32(a: uint32) -> uint32:
    pass

@extern("wide_sum32")
def wide_sum32(a: uint32, b: uint32) -> uint32:
    pass

@extern("wide_scale_to_u16")
def wide_scale_to_u16(x: float, k: float) -> uint16:
    pass


@inline
def report(uart: UART, tag: uint8, value: uint16):
    uart.write(tag)
    uart.write(':')
    uart.print_uint16(value)
    uart.write('\n')


@inline
def report32(uart: UART, tag: uint8, value: uint32):
    uart.write(tag)
    uart.write(':')
    uart.print_uint32(value)
    uart.write('\n')


def main():
    uart = UART(9600)
    uart.println("WIDE")

    # Leaves 0x12, 0x56 and 0x9A in the high halves of R24:R25, R22:R23, R20:R21.
    big: uint16 = wide_echo0(0x1234, 0x5678, 0x9ABC)
    report(uart, 'B', big)

    # Literals that fit in one byte, parameters that do not.
    small: uint16 = wide_sum3(1, 2, 3)
    report(uart, 'S', small)

    # Same again from a uint8 variable: the value is 8-bit, the parameter is not.
    n: uint8 = 200
    widened: uint16 = wide_sum3(n, n, n)
    report(uart, 'V', widened)

    # A 32-bit arg0 crosses in avr-gcc's layout: byte0 in R22, byte3 in R25.
    # With the halves swapped this reads back as 0x00020001 = 131073.
    echoed: uint32 = wide_echo32(0x00010002)
    report32(uart, 'E', echoed)

    # The 32-bit argument in the second slot is contiguous under both layouts.
    total: uint32 = wide_sum32(0x00010002, 1)
    report32(uart, 'T', total)

    # A float literal in a float parameter used to be rounded to an int here, so C
    # multiplied the integer bit pattern read as a float.
    scaled: uint16 = wide_scale_to_u16(1.5, 4.0)
    report(uart, 'F', scaled)

    # An integer literal in a float parameter is promoted, as C would.
    promoted: uint16 = wide_scale_to_u16(3, 2.5)
    report(uart, 'G', promoted)

    uart.println("OK")
    while True:
        pass
