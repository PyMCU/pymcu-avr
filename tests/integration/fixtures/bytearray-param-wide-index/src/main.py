# PyMCU -- bytearray-param-wide-index: a bytearray pointer param indexed past offset 255.
#
# The named-array path (fixtures/array-past-256, pymcu-avr#11) is guarded by NeedsWideIndex,
# but a `buf: bytearray` parameter carries no element count, so BytearrayLoad/BytearrayStore
# loaded the index as a single byte and added the carry into Z's high half against R1 (zero).
# Every offset at and past 256 aliased back onto the first 256 bytes: buf[256] read and wrote
# buf[0]. The SSD1306 framebuffer write hit exactly this -- the control byte 0x40 reappeared
# 256 bytes into the 513-byte transfer.
#
# The dynamic index is a run-time uint16 (seeded from a GPIOR register) so it is not folded,
# and a constant index past 255 checks the literal-immediate path, which dropped the high
# byte of the LDI as well.
#
# Expected UART output:
#   85 0 102
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, uint16


def poke(buf: bytearray, k: uint16):
    buf[k] = 0x55


def poke_far(buf: bytearray):
    buf[300] = 0x66


def peek(buf: bytearray, k: uint16) -> uint8:
    return buf[k]


def main():
    data: uint8[512] = bytearray(512)
    seed: uint16 = GPIOR0.value
    poke(data, seed + 256)
    poke_far(data)
    print(peek(data, seed + 256), data[0], data[300])
    print("done")
