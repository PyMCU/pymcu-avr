# PyMCU -- register-width-widening: an 8-bit register read in a 16-bit context (pymcu-avr#13)
#
# The backend loaded TWO bytes out of an 8-bit register's address whenever the destination
# was wider, so the register ONE BYTE ABOVE arrived as the high half: `d: uint16 =
# GPIOR1.value` emitted `IN R24, 0x2A` then `LDS R25, 0x004B`, which is GPIOR2. The IR was
# always right, asking for a one-byte read widened into a wider destination.
#
# GPIOR1 (0x4A) is the source and GPIOR2 (0x4B) is its neighbour. The test seeds BOTH: the
# neighbour is set to 0xFF, and that is what makes these assertions discriminate. With the
# neighbour left at 0 the wrong code produces the right answer and every check below passes
# while the compiler is still broken.
#
# The answers go to GPIOR0 (0x3E), one per checkpoint:
#   1 -- high byte of `uint16 = GPIOR1.value`      2 -- its low byte
#   3 -- high byte of `uint16 = 100 + GPIOR1.value`
#   4 -- high byte of `uint16 = GPIOR1.value * 2`
#   5 -- byte 1 of `uint32 = GPIOR1.value`
#
# Checkpoints 3 and 4 are the ones that also rule out the lazy fix: with a seed of 200 they
# require a high byte of 1, so a backend that simply cleared the high byte fails them.
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.types import asm, uint8, uint16, uint32


def main() -> None:
    b: uint16 = GPIOR1.value
    GPIOR0.value = (b >> 8) & 0xFF
    asm("BREAK")

    GPIOR0.value = b & 0xFF
    asm("BREAK")

    w: uint16 = 100 + GPIOR1.value
    GPIOR0.value = (w >> 8) & 0xFF
    asm("BREAK")

    m: uint16 = GPIOR1.value * 2
    GPIOR0.value = (m >> 8) & 0xFF
    asm("BREAK")

    q: uint32 = GPIOR1.value
    GPIOR0.value = (q >> 8) & 0xFF
    asm("BREAK")

    while True:
        pass


main()
