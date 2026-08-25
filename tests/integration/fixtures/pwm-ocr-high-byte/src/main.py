# PyMCU -- pwm-ocr-high-byte: a Timer1 duty must not commit a stale TEMP byte.
#
# OCR1A and OCR1B are 16-bit, and every 16-bit timer register on this chip commits
# through one shared TEMP byte: the write of the LOW byte writes TEMP as the high
# byte. The PWM HAL used to write only OCR1AL, so the duty a Timer1 channel got was
# TEMP:duty, where TEMP is whatever the last 16-bit write on Timer1 left behind.
#
# This fixture puts a known value in TEMP with a correct 16-bit write -- the same
# thing the timer and servo HALs do -- and then asks the PWM HAL for a duty. A
# hand-written value is used rather than a real servo pulse because Servo and PWM
# also disagree about the waveform mode, and that would put two variables in one
# measurement; 0x0BB7 is the OCR1A a 1500 us servo pulse produces.
#
# The committed register is read back THROUGH THE CPU, and that is the point of the
# fixture: OCR1AL alone reads back correct on the broken HAL too, because the low
# byte is the one value that always lands. Only the 16-bit register tells the two
# apart. Reading it needs the low byte FIRST, which latches the high byte into TEMP.
#
# Measured: unfixed, a duty of 128 reads back as 2944 (0x0B80) and holds OC1A high
# for 256 of 256 timer ticks; fixed, 128 (0x0080) and 128 of 256.
#
# Data-space addresses (ATmega328P):
#   GPIOR0 = 0x3E   GPIOR1 = 0x4A   GPIOR2 = 0x4B
#   OCR1AL = 0x88   OCR1AH = 0x89
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, OCR1AH, OCR1AL
from pymcu.hal.pwm import PWM
from pymcu.types import asm, uint8


def main():
    duty: uint8 = GPIOR0.value

    # A correct 16-bit write. The high byte lands in TEMP and stays there.
    OCR1AH.value = 0x0B
    OCR1AL.value = 0xB7

    p = PWM("PB1", duty)

    # 16-bit read: low byte first, which latches the high byte into TEMP.
    GPIOR2.value = OCR1AL.value
    GPIOR1.value = OCR1AH.value

    asm("BREAK")
    while True:
        pass
