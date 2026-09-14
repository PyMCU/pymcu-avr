# CircuitPython busio.UART: in_waiting counts, and readinto honours the timeout
# (pymcu-circuitpython#22).
#
# in_waiting was the receive-complete flag, so it answered 0 or 1 however many bytes had
# arrived, and readinto blocked on every byte for ever: a sensor that stopped answering hung
# the program, whatever timeout the constructor was given. The UART is buffered now (the
# HAL's ring and the receive interrupt), so in_waiting is a count and a read that runs out
# of time returns how many bytes it did get.
#
# The test drives the receive line; the firmware reports through GPIOR0..2 at each BREAK:
#   break  GPIOR0                GPIOR1        GPIOR2
#   1      in_waiting            -             -        (before any read)
#   2      bytes readinto got    buf[0]        buf[1]
#
# The 10 ms wait after the first BREAK is the window the test feeds the line in. Without it
# the test has to spend cycles while the firmware sits ON the BREAK, and those cycles carry
# the firmware straight past the next one.
import board
from busio import UART
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, uint16


def main():
    uart = UART(board.TX, board.RX, baudrate=115200, timeout=20)
    buf = bytearray(4)

    asm("BREAK")                      # the test feeds the line during the wait below
    delay_ms(10)
    w: uint16 = uart.in_waiting
    GPIOR0.value = uint8(w)
    asm("BREAK")

    n: uint16 = uart.readinto(buf)
    GPIOR0.value = uint8(n)
    GPIOR1.value = buf[0]
    GPIOR2.value = buf[1]
    asm("BREAK")

    while True:
        pass
