# ATmega328P: uint32(float) must truncate the value, not leak float bits.
#
# Two independent bugs conspired here, found printing 3.25 on a real Uno
# (f"{x:...}" showed a=16387.25 instead of 3.25):
#   1. Optimizer copy propagation treated Copy(FLOAT -> UINT32) as a no-op
#      because both types are 4 bytes wide, so the cast vanished and the
#      consumer received raw float bits (uint32(3.25) printed 16464).
#   2. AvrCodeGen's float-binary path stored the __fixsfsi result with
#      MOV R24,R22 / MOV R25,R23, clobbering the high word before reading
#      it: a 32-bit destination got the low word duplicated
#      (uint32(3.25 * 100.0 + 0.5) stored 0x01450145 = 21299525).
#
# Expected UART output (9600 baud): "325" "325" "3" "3" "40000" then 'D'.
from pymcu.types import uint16, uint32
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    x: float = 3.25
    a: uint32 = uint32(x * 100.0 + 0.5)
    b: uint16 = uint16(x * 100.0 + 0.5)
    c: uint32 = uint32(x)
    d: uint16 = uint16(x)
    y: float = 40000.5
    e: uint32 = uint32(y)
    uart.print_uint32(a)
    uart.print_uint16(b)
    uart.print_uint32(c)
    uart.print_uint16(d)
    uart.print_uint32(e)
    uart.write('D')
