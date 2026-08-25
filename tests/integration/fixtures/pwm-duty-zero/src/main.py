# PyMCU -- pwm-duty-zero: duty 0 has to take the compare output off the pin.
#
# The HAL used to express duty 0 as OCR0A = 0 with the channel left in fast PWM
# non-inverting mode. That is not off: the output is set at BOTTOM and cleared on
# the compare match, so the compare register at BOTTOM never means "no output".
# Off is COM0A1:0 cleared, which returns the pin to normal port operation, and
# the port bit driven low.
#
# The duty is read from GPIOR0 so it is a run-time value. A literal would fold,
# and the folded path is already covered by a compile-time test in PyMCU; what
# needs a running chip is that the branch picks the right side at run time.
#
# The registers under test are read back THROUGH THE CPU into GPIORs, and the
# program then breaks. Watching the pin instead would be asking the simulation
# what it thinks the waveform is; this asks the chip what is in the registers,
# which is what the HAL writes and what a wrong HAL gets wrong. Note that the pin
# cannot be sampled after the break either way -- the CPU is halted there, so the
# timer no longer runs.
#
# Two phases, two breaks:
#   phase 1  the seeded duty, whatever it is
#   phase 2  half duty, so a channel that phase 1 switched off has to come back
#
# Read back at each break:
#   GPIOR1 = TCCR0A   COM0A1:0 in bits 7:6 -- 10 is connected, 00 is off
#   GPIOR2 = OCR0A    the compare value, which must NOT be how off is expressed
#   GPIOR0 = PORTD    bit 6 is OC0A; it holds the pin once the compare is off
#
# Data-space addresses (ATmega328P):
#   GPIOR0 = 0x3E   GPIOR1 = 0x4A   GPIOR2 = 0x4B
#   TCCR0A = 0x44   TCCR0B = 0x45   OCR0A  = 0x47   PORTD = 0x2B
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, TCCR0A, OCR0A, PORTD
from pymcu.hal.pwm import PWM
from pymcu.types import asm, uint8


def main():
    duty: uint8 = GPIOR0.value

    pwm = PWM("PD6", duty)
    pwm.set_duty(duty)

    GPIOR1.value = TCCR0A.value
    GPIOR2.value = OCR0A.value
    GPIOR0.value = PORTD.value
    asm("BREAK")

    pwm.set_duty(128)

    GPIOR1.value = TCCR0A.value
    GPIOR2.value = OCR0A.value
    GPIOR0.value = PORTD.value
    asm("BREAK")

    while True:
        pass
