# CircuitPython countio.Counter: how many edges arrived on a pin (pymcu-circuitpython#11).
#
# The module was absent, so a flow meter or a tachometer had no way in. The mechanism is a
# pin interrupt and a 32-bit counter, not a hardware counter: every timer on this part is
# already spoken for, and a timer's external clock input is T0 (D4) or T1 (D5) and nothing
# else, while an interrupt counts on any of 23 pins.
#
# The test pulses D2 during the 20 ms wait and reads back:
#   break  GPIOR0            GPIOR1            GPIOR2
#   2      count low byte    count high byte   -
#   3      count after reset -                 -
import board
import countio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, uint32


def main():
    c = countio.Counter(board.D2, edge=countio.Edge.FALL)

    asm("BREAK")               # the test pulses the pin during the wait below
    delay_ms(20)

    n: uint32 = c.count
    GPIOR0.value = uint8(n & 0xFF)
    GPIOR1.value = uint8((n >> 8) & 0xFF)
    GPIOR2.value = 0
    asm("BREAK")

    c.reset()
    n = c.count
    GPIOR0.value = uint8(n & 0xFF)
    asm("BREAK")

    while True:
        pass
