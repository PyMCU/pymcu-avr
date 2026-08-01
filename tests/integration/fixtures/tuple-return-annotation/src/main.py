# PyMCU -- tuple-return-annotation: the `-> (T1, T2)` multi-value return annotation
#
# Demonstrates:
#   - `-> (uint8, uint8)` on an @inline function, unpacked at the call site
#   - the equivalent `-> tuple[uint8, uint8]` spelling
#   - `-> (uint8, uint16)`: the annotated element widths reach the result slots,
#     so the wider value is not truncated to 8 bits
#
# opaque() is a real subroutine, so its result is not a compile-time constant and
# the arithmetic below is genuinely evaluated at runtime.
#
# Output on UART (9600 baud):
#   "TA\n"      -- boot banner
#   "Q:03\n"    -- divmod8(10, 3) quotient = 3
#   "R:01\n"    -- divmod8(10, 3) remainder = 1
#   "H:02\n"    -- split16(0x022C) high byte = 0x02
#   "L:2C\n"    -- split16(0x022C) low byte  = 0x2C
#   "N:02\n"    -- scale(2) first element = 2
#   "S:02\n"    -- scale(2) second element 2*300 = 600 = 0x0258, high byte
#   "T:58\n"    -- scale(2) second element, low byte
#
from pymcu.types import uint8, uint16, inline
from pymcu.hal.uart import UART


def nibble_hi(val: uint8) -> uint8:
    n: uint8 = (val >> 4) & 0x0F
    if n < 10:
        return n + 48
    return n + 55


def nibble_lo(val: uint8) -> uint8:
    n: uint8 = val & 0x0F
    if n < 10:
        return n + 48
    return n + 55


def opaque(v: uint8) -> uint8:
    return v


@inline
def divmod8(a: uint8, b: uint8) -> (uint8, uint8):
    q: uint8 = a // b
    r: uint8 = a - (q * b)
    return (q, r)


@inline
def split16(v: uint16) -> tuple[uint8, uint8]:
    return ((v >> 8) & 0xFF, v & 0xFF)


@inline
def scale(n: uint8) -> (uint8, uint16):
    return (n, n * 300)


def main():
    uart = UART(9600)

    uart.println("TA")

    # -- `-> (uint8, uint8)` on @inline, unpacked --
    num: uint8 = opaque(10)
    quot: uint8 = 0
    rem: uint8 = 0
    quot, rem = divmod8(num, 3)
    uart.write('Q')
    uart.write(':')
    uart.write(nibble_hi(quot))
    uart.write(nibble_lo(quot))
    uart.write('\n')
    uart.write('R')
    uart.write(':')
    uart.write(nibble_hi(rem))
    uart.write(nibble_lo(rem))
    uart.write('\n')

    # -- the `tuple[...]` spelling of the same annotation --
    wide: uint16 = 0x0200 + opaque(0x2C)
    hi: uint8 = 0
    lo: uint8 = 0
    hi, lo = split16(wide)
    uart.write('H')
    uart.write(':')
    uart.write(nibble_hi(hi))
    uart.write(nibble_lo(hi))
    uart.write('\n')
    uart.write('L')
    uart.write(':')
    uart.write(nibble_hi(lo))
    uart.write(nibble_lo(lo))
    uart.write('\n')

    # -- mixed widths: the uint16 element must not be truncated --
    seed: uint8 = opaque(2)
    first: uint8 = 0
    prod: uint16 = 0
    first, prod = scale(seed)
    uart.write('N')
    uart.write(':')
    uart.write(nibble_hi(first))
    uart.write(nibble_lo(first))
    uart.write('\n')
    prod_hi: uint8 = (prod >> 8) & 0xFF
    prod_lo: uint8 = prod & 0xFF
    uart.write('S')
    uart.write(':')
    uart.write(nibble_hi(prod_hi))
    uart.write(nibble_lo(prod_hi))
    uart.write('\n')
    uart.write('T')
    uart.write(':')
    uart.write(nibble_hi(prod_lo))
    uart.write(nibble_lo(prod_lo))
    uart.write('\n')

    while True:
        pass
