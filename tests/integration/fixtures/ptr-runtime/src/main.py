# ptr(BASE + offset) with a runtime offset, exercised through .value:
# write, augmented-assign (read-modify-write), and read all lower to Store/Load
# indirect through the computed address. Round-trips a value via a free SRAM slot.
from pymcu.types import uint8, uint16, ptr, const
from pymcu.hal.uart import UART

BASE: const[uint16] = 0x0500   # free SRAM on ATmega328P (0x0100..0x08FF)


def main():
    uart = UART(9600)
    uart.println("PR")

    off: uint8 = 4
    slot: ptr[uint8] = ptr(BASE + off)   # runtime-offset pointer (via variable)
    slot.value = 40                      # StoreIndirect 40
    slot.value += 2                      # LoadIndirect + 2 + StoreIndirect -> 42

    # Inline form: read back through a freshly computed pointer to the same address.
    result: uint8 = ptr(BASE + off).value
    uart.write(result)                   # expect 42

    while True:
        pass
