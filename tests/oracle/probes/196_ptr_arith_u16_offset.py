# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16, ptr, const

BASE: const[uint16] = 0x0400   # free SRAM on ATmega328P (0x0100..0x08FF)

off: uint16 = GPIOR0.value + 256
# write through a runtime-computed address, read through a constant one --
# a high-byte drop on either side makes the two disagree.
slot: ptr[uint8] = ptr(BASE + off)
slot.value = 55
back: uint8 = ptr(BASE + 256).value
print(back)
print("END")
