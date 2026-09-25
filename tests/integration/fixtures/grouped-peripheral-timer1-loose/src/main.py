# PyMCU -- grouped-peripheral-timer1-loose: Timer/Counter1 through the loose names.
#
# RFC 0012. The same program is written twice: here against the grouped peripheral
# (TCCR1A, 0) and here against the module-level register names. The two must produce the SAME firmware, byte for byte,
# The grouped copy is the one the emulator drives; this one pins the byte identity.
#
# Fast PWM, mode 14 (WGM13:12 = 11, WGM11 = 1, WGM10 = 0), TOP = ICR1, non-inverting
# output on OC1A (PB1), prescaler 1. At 16 MHz an ICR1 of 19999 makes one period
# 20000 cycles = 1.25 ms.
#
# Checkpoint 1: the timer is configured and nothing has counted yet.
# Checkpoint 2: three overflows have been seen and cleared, counted into GPIOR0.
# Checkpoint 3: the clock is stopped and the counter is copied, through the 16-bit
#               name, into OCR1B; its low byte goes to GPIOR0 through the byte name.
from pymcu.chips.atmega328p import (
    TCCR1A, TCCR1B, TCNT1, TCNT1L, OCR1A, OCR1B, ICR1, TIMSK1, TIFR1,
    DDRB, GPIOR0,
)
from pymcu.types import uint8, uint16, asm


def main():
    DDRB[1] = 1                      # OC1A is PB1

    TCNT1.value = 0
    ICR1.value = 19999        # TOP
    OCR1A.value = 1500        # duty of OC1A
    OCR1B.value = 1000        # duty of OC1B
    TCCR1A.value = (1 << 7) | (1 << 1)
    TCCR1B.value = (1 << 4) | (1 << 3) | (1 << 0)
    TIMSK1.value = 0          # polled, no interrupt
    TIFR1[0] = 1    # a flag is cleared by writing a ONE to it
    asm("BREAK")

    overflows: uint8 = 0
    while overflows < 3:
        if TIFR1[0]:
            TIFR1[0] = 1
            overflows = overflows + 1
    GPIOR0.value = overflows
    asm("BREAK")

    # Stop the clock so the counter holds still, then read it back at both widths.
    TCCR1B.value = (1 << 4) | (1 << 3)
    count: uint16 = TCNT1.value
    OCR1B.value = count
    GPIOR0.value = TCNT1L.value
    asm("BREAK")

    while True:
        pass
