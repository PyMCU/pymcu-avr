# Access WIDTH of Store/LoadIndirect through a runtime ptr[T] is the pointer's
# element type, not the stored value's magnitude. A small constant written through
# a ptr[uint16]/ptr[uint32] must still write every element byte: the optimizer
# copy-forwards the constant into the store, and a Constant's own type is only as
# wide as its value. Surrounding bytes are poisoned first so a too-narrow store
# (or a too-narrow read-modify-write) leaves visible garbage.
from pymcu.types import uint8, uint16, uint32, ptr, const
from pymcu.hal.uart import UART

BASE: const[uint16] = 0x0500   # free SRAM on ATmega328P (0x0100..0x08FF)


def poison(a: uint16, n: uint8):
    i: uint8 = 0
    while i < n:
        b: ptr[uint8] = ptr(a + i)
        b.value = 0xEE
        i = i + 1


def store16(off: uint16):
    p: ptr[uint16] = ptr(BASE + off)
    p.value = 0x12             # 1-byte magnitude: must still write BOTH bytes


def aug16(off: uint16):
    p: ptr[uint16] = ptr(BASE + off)
    p.value += 1               # 16-bit read + add + 16-bit write


def store32(off: uint16):
    p: ptr[uint32] = ptr(BASE + 0x20 + off)
    p.value = 0x34             # 1-byte magnitude: must still write all FOUR bytes


def load8(a: uint16) -> uint8:
    q: ptr[uint8] = ptr(a)
    return q.value


def store8(a: uint16, v: uint8):
    q: ptr[uint8] = ptr(a)
    q.value = v


def main():
    uart = UART(9600)
    uart.println("PW")

    # 16-bit store of a small constant: high byte must be written (0x00, not 0xEE).
    poison(BASE + 8, 2)
    store16(8)
    uart.write(load8(BASE + 8))        # expect 0x12
    uart.write(load8(BASE + 9))        # expect 0x00

    # 16-bit read-modify-write: seed 0x0112 byte-wise, += 1 -> 0x0113. A too-narrow
    # LOAD reads 0x12, adds 1 and stores 0x0013 - the high byte would drop to 0x00.
    store8(BASE + 8, 0x12)
    store8(BASE + 9, 0x01)
    aug16(8)
    uart.write(load8(BASE + 8))        # expect 0x13
    uart.write(load8(BASE + 9))        # expect 0x01

    # 32-bit store of a small constant: all three high bytes must be written.
    poison(BASE + 0x20 + 8, 4)
    store32(8)
    uart.write(load8(BASE + 0x20 + 8))   # expect 0x34
    uart.write(load8(BASE + 0x20 + 9))   # expect 0x00
    uart.write(load8(BASE + 0x20 + 10))  # expect 0x00
    uart.write(load8(BASE + 0x20 + 11))  # expect 0x00

    while True:
        pass
