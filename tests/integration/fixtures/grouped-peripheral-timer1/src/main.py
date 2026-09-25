# PyMCU -- grouped-peripheral-timer1: Timer/Counter1 reached through the TIMER1 group.
#
# RFC 0012. The same program is written twice: here against the grouped peripheral
# (TIMER1.TCCR1A, TIMER1.CS10) and in grouped-peripheral-timer1-loose against the
# module-level register names. The two must produce the SAME firmware, byte for byte,
# and this one must configure the timer for real.
#
# Fast PWM, mode 14 (WGM13:12 = 11, WGM11 = 1, WGM10 = 0), TOP = ICR1, non-inverting
# output on OC1A (PB1), prescaler 1. At 16 MHz an ICR1 of 19999 makes one period
# 20000 cycles = 1.25 ms.
#
# Checkpoint 1: the timer is configured and nothing has counted yet.
# Checkpoint 2: three overflows have been seen and cleared, counted into GPIOR0.
# Checkpoint 3: the clock is stopped and the counter is copied, through the 16-bit
#               name, into OCR1B; its low byte goes to GPIOR0 through the byte name.
from pymcu.chips.atmega328p import TIMER1, DDRB, GPIOR0
from pymcu.types import uint8, uint16, asm


def main():
    DDRB[1] = 1                      # OC1A is PB1

    TIMER1.TCNT1.value = 0
    TIMER1.ICR1.value = 19999        # TOP
    TIMER1.OCR1A.value = 1500        # duty of OC1A
    TIMER1.OCR1B.value = 1000        # duty of OC1B
    TIMER1.TCCR1A.value = (1 << TIMER1.COM1A1) | (1 << TIMER1.WGM11)
    TIMER1.TCCR1B.value = (1 << TIMER1.WGM13) | (1 << TIMER1.WGM12) | (1 << TIMER1.CS10)
    TIMER1.TIMSK1.value = 0          # polled, no interrupt
    TIMER1.TIFR1[TIMER1.TOV1] = 1    # a flag is cleared by writing a ONE to it
    asm("BREAK")

    overflows: uint8 = 0
    while overflows < 3:
        if TIMER1.TIFR1[TIMER1.TOV1]:
            TIMER1.TIFR1[TIMER1.TOV1] = 1
            overflows = overflows + 1
    GPIOR0.value = overflows
    asm("BREAK")

    # Stop the clock so the counter holds still, then read it back at both widths.
    TIMER1.TCCR1B.value = (1 << TIMER1.WGM13) | (1 << TIMER1.WGM12)
    count: uint16 = TIMER1.TCNT1.value
    TIMER1.OCR1B.value = count
    GPIOR0.value = TIMER1.TCNT1L.value
    asm("BREAK")

    while True:
        pass
