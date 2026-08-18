# ATmega328P: zero-initialized mutable globals must be written at startup.
#
# The frontend used to skip `counter: uint16 = 0` module inits, trusting BSS
# zeroing -- but the AVR backend may give a mutable global a register home
# (R2-R15), which the BSS loop never touches, and AVR registers power up
# undefined. On a real Uno the global started at whatever the bootloader left
# in those registers (14087 observed); the emulator zeroes registers on reset,
# which is why only silicon showed it. The companion test dirties R2-R15
# before running to reproduce the hardware condition.
#
# counter is mutated from two functions so the register allocator homes it.
#
# Expected UART output (9600 baud): "c0=0", "c2=14", "b=101", then 'D'.
from pymcu.types import uint16
from pymcu.hal.uart import UART
from pymcu.time import delay_ms

counter: uint16 = 0
base: uint16 = 100

uart = UART(9600)


def tick():
    global counter
    counter = counter + 7


def rebase() -> uint16:
    global base
    base = base + 1
    return base


def main():
    uart.write_str("c0=")
    uart.print_uint16(counter)
    tick()
    tick()
    uart.write_str("c2=")
    uart.print_uint16(counter)
    uart.write_str("b=")
    uart.print_uint16(rebase())
    uart.write('D')
    while True:
        delay_ms(1000)
