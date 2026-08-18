# ATmega328P: a constant operand wider than 16 bits must widen the operation.
#
# The backend typed every Constant above 255 as UINT16, so a runtime division
# with a folded 32-bit constant operand ran as 16-bit and silently truncated:
# (1100 * 1024) // avg computed 12288 // avg (1126400 mod 2^16) and returned
# 53 instead of 4897. Found reading the internal 1.1 V bandgap on a real Uno
# (the Vcc formula printed 53 mV). Constants now type by value, forcing the
# 32-bit division.
#
# The accumulator loop keeps avg out of compile-time constant folding, which
# is what hid the bug from simpler expressions.
#
# Expected UART output (9600 baud): "4897" then 'D'.
from pymcu.types import uint8, uint16, uint32
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)

    n: uint8 = 0
    acc: uint32 = 0
    while n < 16:
        acc = acc + 230
        n = n + 1
    avg: uint16 = acc // 16
    v: uint16 = (1100 * 1024) // avg
    uart.print_uint16(v)
    uart.write('D')

    while True:
        delay_ms(1000)
