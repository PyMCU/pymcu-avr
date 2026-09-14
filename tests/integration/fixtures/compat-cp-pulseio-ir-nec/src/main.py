# An infrared NEC frame, captured and decoded (pymcu-circuitpython#9).
#
# This is what pulseio.PulseIn is for. A demodulating receiver hands the pin one edge per
# mark and per space, and the frame is read entirely out of how long they were:
#
#   9000 us mark, 4500 us space, then 32 bits, each a 560 us mark followed by a space --
#   560 us for a zero, 1690 us for a one -- and a 560 us stop mark. Least significant bit
#   first, address then its complement then command then its complement.
#
# The test drives D2 with a frame for address 0x04 command 0x08 and reads back:
#   GPIOR0 = how many pulses arrived   GPIOR1 = address   GPIOR2 = command
import board
import pulseio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, uint16


def main():
    pulses = pulseio.PulseIn(board.D2, maxlen=70)

    asm("BREAK")                 # the test sends the frame during the wait below
    delay_ms(120)

    n: uint16 = len(pulses)
    GPIOR0.value = uint8(n)

    # The leader is pulses[0] and pulses[1]; the spaces that carry the bits are every
    # other pulse from index 3.
    address: uint8 = 0
    command: uint8 = 0
    i: uint8 = 0
    while i < 8:
        if pulses[3 + 2 * i] > 1000:
            address = address | (1 << i)
        if pulses[3 + 2 * (i + 16)] > 1000:
            command = command | (1 << i)
        i = i + 1

    GPIOR1.value = address
    GPIOR2.value = command
    asm("BREAK")

    while True:
        pass
