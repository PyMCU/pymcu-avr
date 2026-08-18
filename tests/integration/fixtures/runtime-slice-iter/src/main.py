# for-in over an array slice whose bounds are only known at runtime: rewritten
# to the equivalent range loop with a per-iteration ArrayLoad (no allocation is
# needed to ITERATE a slice). Covers a runtime stop, a runtime expression on
# both bounds, and break inside the rewritten body.
#
# Expected UART output (9600 baud): "10" "67" "68" "66" then 'D'.
from pymcu.hal.uart import UART
from pymcu.types import uint8


def fill(buf: bytearray) -> uint8:
    i: uint8 = 0
    while i < 6:
        buf[i] = 65 + i
        i = i + 1
    return 4


def main():
    uart = UART(9600)
    buf: bytearray = bytearray(6)
    n: uint8 = fill(buf)

    total: uint8 = 0
    for b in buf[0:n]:
        total = total + b
    uart.print_uint16(total)

    for b in buf[n - 2:n]:
        uart.print_uint16(b)

    for b in buf[1:n + 1]:
        if b > 66:
            break
        uart.print_uint16(b)

    uart.write('D')
