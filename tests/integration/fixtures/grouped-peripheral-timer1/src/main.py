# PyMCU -- grouped-peripheral-timer1: Timer/Counter1 reached through the Timer1 group.
#
# RFC 0012. The same program is written twice: here against the grouped peripheral
# (Timer1.TCCR1A, Timer1.CS10) and in grouped-peripheral-timer1-loose against the
# module-level register names. The two must produce the SAME firmware, byte for byte,
# and this one must configure the timer for real.
#
# Fast PWM, mode 14 (WGM13:12 = 11, WGM11 = 1, WGM10 = 0), TOP = ICR1, non-inverting
# output on OC1A (PB1), prescaler 1. At 16 MHz an ICR1 of 19999 makes one period
# 20000 cycles = 1.25 ms.
#
# Every 16-bit register of Timer1 is WRITTEN through its byte names, high half first.
# The AVR commits such a register when the LOW byte is written and takes the high half
# from a temporary register shared by the whole timer, so the high byte has to be put
# there first. This is what the HAL does (lib/src/pymcu/hal/avr/pwm/atmega328p.py) and
# what the fixture does, so the timer really runs at the period the test claims.
# READING is the other order and the 16-bit name is correct for it, which checkpoint 3
# exercises.
#
# Checkpoint 1: the timer is configured and nothing has counted yet.
# Checkpoint 2: three overflows have been seen and cleared, counted into GPIOR0.
# Checkpoint 3: the clock is stopped, the counter is given a value whose halves differ,
#               and it is read back through the 16-bit name and through both byte names.
from pymcu.chips.atmega328p import Timer1, DDRB, GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, uint16, asm


def main():
    DDRB[1] = 1                      # OC1A is PB1

    Timer1.TCNT1H.value = 0
    Timer1.TCNT1L.value = 0
    Timer1.ICR1H.value = 0x4E        # TOP = 19999
    Timer1.ICR1L.value = 0x1F
    Timer1.OCR1AH.value = 0x05       # duty of OC1A = 1500
    Timer1.OCR1AL.value = 0xDC
    Timer1.OCR1BH.value = 0x03       # duty of OC1B = 1000
    Timer1.OCR1BL.value = 0xE8
    Timer1.TCCR1A.value = (1 << Timer1.COM1A1) | (1 << Timer1.WGM11)
    Timer1.TCCR1B.value = (1 << Timer1.WGM13) | (1 << Timer1.WGM12) | (1 << Timer1.CS10)
    Timer1.TIMSK1.value = 0          # polled, no interrupt
    Timer1.TIFR1[Timer1.TOV1] = 1    # a flag is cleared by writing a ONE to it
    asm("BREAK")

    overflows: uint8 = 0
    while overflows < 3:
        if Timer1.TIFR1[Timer1.TOV1]:
            Timer1.TIFR1[Timer1.TOV1] = 1
            overflows = overflows + 1
    GPIOR0.value = overflows
    asm("BREAK")

    # Stop the clock and seed the counter, then read it back at both widths.
    Timer1.TCCR1B.value = (1 << Timer1.WGM13) | (1 << Timer1.WGM12)
    Timer1.TCNT1H.value = 0x12
    Timer1.TCNT1L.value = 0x34
    count: uint16 = Timer1.TCNT1.value
    GPIOR0.value = 0
    if count == 0x1234:
        GPIOR0.value = 0xA5
    GPIOR1.value = Timer1.TCNT1L.value
    GPIOR2.value = Timer1.TCNT1H.value
    asm("BREAK")

    while True:
        pass
