# ATmega328P: joining two bytes into a 16-bit word must stay 16-bit.
#
# Arithmetic promotion widened by STORAGE type, so `hi * 256` with hi:uint8
# became uint32 even though it cannot exceed 65280, and `lo + hi * 256` then
# ran 32-bit: AvrCodeGen spilled the four bytes to the frame (Y+2..Y+5) and
# reloaded only the low half. The HAL's ADC and Timer1 reads pay this on every
# call -- 36 bytes of flash where the concatenation itself is two MOVs.
# Promotion is now skipped when the result provably fits the unpromoted type.
#
# The accumulator loop keeps lo/hi/big out of compile-time constant folding.
# `big * 256` guards the other direction: a uint16 operand CAN overflow 16 bits,
# so that one must still widen. 300 * 256 = 76800 = 0x012C00, printed as its two
# halves -- a result wrongly kept at 16 bits would report a high half of 0.
#
# Expected UART output (9600 baud): "4660\n4660\n4660\n11264\n1\n" then 'D'.
from pymcu.types import uint8, uint16, uint32
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)

    n: uint8 = 0
    lo: uint8 = 0
    hi: uint8 = 0
    big: uint16 = 0
    while n < 2:
        lo = lo + 26        # 0x34
        hi = hi + 9         # 0x12
        big = big + 150     # 300
        n = n + 1

    a: uint16 = lo + hi * 256
    b: uint16 = hi * 256 + lo
    c: uint16 = (hi << 8) | lo
    wide: uint32 = big * 256

    uart.print_uint16(a)
    uart.print_uint16(b)
    uart.print_uint16(c)
    uart.print_uint16(uint16(wide))
    uart.print_uint16(uint16(wide >> 16))
    uart.write('D')

    while True:
        delay_ms(1000)
