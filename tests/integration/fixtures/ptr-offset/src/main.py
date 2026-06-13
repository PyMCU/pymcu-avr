# ptr(BASE + offset): compile-time address arithmetic on a register base.
# A bare register name contributes its ADDRESS (not its dereferenced value), so
# PINB(0x23) + 2 resolves to PORTB(0x25). Driving that pointer must set PORTB.
from pymcu.types import uint8, ptr
from pymcu.chips.atmega328p import DDRB, PINB


def main():
    DDRB.value = 0xFF                  # all PORTB pins outputs
    port: ptr[uint8] = ptr(PINB + 2)   # 0x23 + 2 = 0x25 = PORTB
    port.value = 0xFF                  # drive PORTB high via the computed pointer
    while True:
        pass
