# CircuitPython pwmio: deinit() releases the pin (PyMCU#296).
#
# CircuitPython leaves a deinit'd PWMOut pin as an input. PyMCU stopped the timer and
# left the compare output connected, so D6 stayed at whatever level OC0A had at that
# instant (5 V about half the time, measured on an Arduino Uno with a scope), and the
# sibling channel on D5 froze with it.
#
# Read back through the CPU into the GPIORs at two breaks:
#   break 1  GPIOR0 = DDRD   GPIOR1 = TCCR0A   GPIOR2 = TCCR0B
#   break 2  GPIOR0 = PORTD
#
import board
import pwmio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, TCCR0A, TCCR0B, PORTD, DDRD
from pymcu.types import asm


def main():
    a = pwmio.PWMOut(board.D6, duty_cycle=32768)   # OC0A
    b = pwmio.PWMOut(board.D5, duty_cycle=16384)   # OC0B, the sibling channel
    a.deinit()

    GPIOR0.value = DDRD.value
    GPIOR1.value = TCCR0A.value
    GPIOR2.value = TCCR0B.value
    asm("BREAK")

    GPIOR0.value = PORTD.value
    asm("BREAK")

    while True:
        pass
