# PyMCU -- pwm-timer0-timebase: a PWM on Timer0 next to the time base (PyMCU#295).
#
# Timer0 is shared three ways on the ATmega328P: OC0A (PD6), OC0B (PD5) and the overflow
# that clocks millis()/micros(). Measured on an Arduino Uno: PWM("PD6", 128, 5000) after
# millis_init() made monotonic() run 8.44 times too fast. That request is now refused at
# compile time (tests/stdlib/test_pwm_timebase_conflict.py in PyMCU); what runs here is
# what is allowed, and it has to leave the clock alone in both orders.
#
# millis_init() used to clear TCCR0A, which killed a PWM built before it: fast PWM
# overflows at the same rate as normal mode, so it now leaves the register alone.
#
# GPIOR0 selects the scenario:
#   0  millis_init(), then PWM("PD6", 128)   -> dt 100, A 131, B 3
#   1  PWM("PD6", 128), then millis_init()   -> dt 100, A 131, B 3 (mode kept)
from pymcu.chips.atmega328p import GPIOR0, TCCR0A, TCCR0B
from pymcu.hal.console import print
from pymcu.hal.timer import millis_init, millis
from pymcu.hal.pwm import PWM
from pymcu.time import delay_ms
from pymcu.types import uint8, uint32


def main():
    scenario: uint8 = GPIOR0.value
    if scenario == 0:
        millis_init()
        a0 = PWM("PD6", 128)
    else:
        a1 = PWM("PD6", 128)
        millis_init()
    t0: uint32 = millis()
    delay_ms(100)
    print("dt", millis() - t0)
    print("A", TCCR0A.value, "B", TCCR0B.value)
    print("END")
