# CircuitPython supervisor module integration test fixture
#
# Verifies that supervisor.ticks_ms() compiles and returns a small value right
# after boot. CircuitPython does not promise ticks_ms() == 0 at boot -- its
# documented guarantee is the DIFFERENCE between two reads, not any one
# reading's absolute value -- and millis() (which this stub now calls) folds
# in the in-progress Timer0 overflow down to the cycle, so even the very
# first read, a handful of instructions after millis_init() arms the timer,
# can legitimately see 1 or 2 ticks already elapsed. The exact count depends
# on how many cycles the optimized vs unoptimized build takes to reach this
# line, so the test checks a small bound instead of an exact 0.
#
# Expected UART output:
#   Byte 0: low byte of ticks_ms() result (small, boot-time value)
#   Byte 1: 0x44 ('D') -- done marker
#
import board
import busio
import supervisor
from pymcu.types import uint8, uint32


def main():
    uart = busio.UART(board.TX, board.RX, baudrate=9600)
    t: uint32 = supervisor.ticks_ms()   # small value shortly after boot
    buf: uint8[1]
    buf[0] = t & 0xFF
    uart.write(buf)       # a few, not necessarily 0
    uart.write(b"D")      # 'D' done marker
    while True:
        pass
