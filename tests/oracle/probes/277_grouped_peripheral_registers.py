# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import ptr, uint8, const

BASE: const[uint8] = 0x04   # a byte of free SRAM, 0x0400 on the ATmega328P


# RFC 0012: a peripheral's registers grouped as attributes of one class. The
# grouping is compile-time only, so every access below must read and write the
# same bytes the loose names would.
class BLOCK:
    CTRL: ptr[uint8] = ptr(BASE * 256)
    STATUS: ptr[uint8] = ptr(BASE * 256 + 1)

    RUN: int = 0
    READY: int = 3


seed: uint8 = GPIOR0.value
BLOCK.CTRL.value = 0
BLOCK.STATUS.value = seed

BLOCK.CTRL[BLOCK.RUN] = 1
BLOCK.CTRL[BLOCK.READY] = 1
print(BLOCK.CTRL.value)

if BLOCK.CTRL[BLOCK.RUN]:
    print("running")
if BLOCK.CTRL[BLOCK.READY]:
    print("ready")

BLOCK.CTRL[BLOCK.RUN] = 0
print(BLOCK.CTRL.value)
print(BLOCK.STATUS.value)

BLOCK.STATUS.value = BLOCK.STATUS.value + 1
print(BLOCK.STATUS.value)
print("END")
