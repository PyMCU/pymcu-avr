# PyMCU -- pwm-stop-keeps-timer: stop() takes the channel off the pin, not the timer
# off the chip (PyMCU#296).
#
# PWM.stop() used to write TCCRxB = 0. That stops the whole timer: the sibling channel
# on the same timer freezes with it, Timer0's overflow (the time base behind
# monotonic() and ticks_ms()) stops counting, and this channel's compare output stays
# connected, so the pin keeps whatever level the OCxA latch had when the clock went
# away. Measured on an Arduino Uno with a scope: after deinit() D6 sits at 5 V about
# half the time.
#
# Off is the compare output disconnected and the pin driven low, exactly what duty 0
# already does; the timer keeps running. start() reconnects the output unless the duty
# is 0, and deinit() additionally returns the pin to an input.
#
# The duty is seeded from GPIOR0 (a run-time value; a literal would fold the start()
# branch away). Registers are read back THROUGH THE CPU into the GPIORs at three
# breaks, the way pwm-duty-zero does it:
#
#   break 1  after a.stop()    GPIOR1 = TCCR0A   GPIOR2 = TCCR0B   GPIOR0 = PORTD
#   break 2  after a.start()   GPIOR1 = TCCR0A   GPIOR2 = TCCR0B   GPIOR0 = PORTD
#   break 3  after a.deinit()  GPIOR1 = DDRD     GPIOR2 = TCCR0A   GPIOR0 = PORTD
#
# Data-space addresses (ATmega328P):
#   GPIOR0 = 0x3E   GPIOR1 = 0x4A   GPIOR2 = 0x4B
#   TCCR0A = 0x44   TCCR0B = 0x45   PORTD = 0x2B   DDRD = 0x2A
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, TCCR0A, TCCR0B, PORTD, DDRD
from pymcu.hal.pwm import PWM
from pymcu.types import asm, uint8


def main():
    duty: uint8 = GPIOR0.value

    a = PWM("PD6", duty)     # Timer0 OC0A, the channel under test
    b = PWM("PD5", 64)       # Timer0 OC0B, its sibling, must not notice

    a.stop()
    GPIOR1.value = TCCR0A.value
    GPIOR2.value = TCCR0B.value
    GPIOR0.value = PORTD.value
    asm("BREAK")

    a.start()
    GPIOR1.value = TCCR0A.value
    GPIOR2.value = TCCR0B.value
    GPIOR0.value = PORTD.value
    asm("BREAK")

    a.deinit()
    GPIOR1.value = DDRD.value
    GPIOR2.value = TCCR0A.value
    GPIOR0.value = PORTD.value
    asm("BREAK")

    while True:
        pass
