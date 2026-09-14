# CircuitPython pwmio: the 16-bit duty lands exactly (pymcu-circuitpython#30).
#
# Fast PWM is high for OCR + 1 of 256 counts. duty_cycle >> 8 straight into OCR put
# every duty 1/256 above what was asked: measured on an Arduino Uno, 32768 read 50.4 %
# and the 50 Hz servo pulse 7.8 % instead of 7.5 %. The HAL now rounds the 16-bit duty
# to a number of counts and programs one less; 0 counts is off, 256 is fully on.
#
# The duty is seeded from GPIOR0 (0..4 selects a value) so the setter runs at run time;
# the constructor uses the literal. Read back at BREAK:
#   GPIOR1 = OCR0A   GPIOR2 = TCCR0A (COM0A1:0 in bits 7:6, 10 connected, 00 off)
#
#   seed  duty_cycle  counts  OCR0A  COM0A
#   0     32768       128     127    10
#   1     65535       256     255    10
#   2     127         0       0      00  (off, below half a count)
#   3     128         1       0      10  (0.39 %, the smallest pulse)
#   4     16384       64      63     10
import board
import pwmio
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, OCR0A, TCCR0A
from pymcu.types import asm, uint8, uint16


def main():
    led = pwmio.PWMOut(board.D6, duty_cycle=32768)
    GPIOR1.value = OCR0A.value
    GPIOR2.value = TCCR0A.value
    asm("BREAK")

    seed: uint8 = GPIOR0.value
    want: uint16 = 32768
    if seed == 1:
        want = 65535
    elif seed == 2:
        want = 127
    elif seed == 3:
        want = 128
    elif seed == 4:
        want = 16384
    led.duty_cycle = want
    GPIOR1.value = OCR0A.value
    GPIOR2.value = TCCR0A.value
    asm("BREAK")

    while True:
        pass
