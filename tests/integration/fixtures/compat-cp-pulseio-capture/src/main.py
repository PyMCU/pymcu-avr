# CircuitPython pulseio.PulseIn: the pulses on a pin, measured (pymcu-circuitpython#9).
#
# The module did not exist, so the two things it is for -- an infrared receiver and a DHT
# on any digital pin -- had no way in. Timer1 runs free at prescaler 8 and a pin-change
# interrupt on D2 timestamps every edge; the length of a pulse is the difference.
#
# The test drives D2 during the 40 ms wait after the first BREAK, then reads back:
#   break  GPIOR0            GPIOR1           GPIOR2
#   2      len(pulses)       pulses[0] low    pulses[0] high
#   3      len(pulses)       pulses[1] low    pulses[1] high
#   4      popleft() low     popleft() high   len(pulses) after two popleft
import board
import pulseio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.time import delay_ms
from pymcu.types import asm, uint8, uint16


def main():
    pulses = pulseio.PulseIn(board.D2, maxlen=8)

    asm("BREAK")               # the test drives the line during the wait below
    delay_ms(40)

    n: uint16 = len(pulses)
    a: uint16 = pulses[0]
    GPIOR0.value = uint8(n)
    GPIOR1.value = uint8(a & 0xFF)
    GPIOR2.value = uint8(a >> 8)
    asm("BREAK")

    b: uint16 = pulses[1]
    GPIOR0.value = uint8(n)
    GPIOR1.value = uint8(b & 0xFF)
    GPIOR2.value = uint8(b >> 8)
    asm("BREAK")

    c: uint16 = pulses.popleft()
    d: uint16 = pulses.popleft()
    GPIOR0.value = uint8(c & 0xFF)
    GPIOR1.value = uint8(c >> 8)
    GPIOR2.value = uint8(len(pulses))
    asm("BREAK")

    while True:
        pass
