# PyMCU -- compat-mp-pwm-surface: the machine.PWM spellings MicroPython programs use
# (pymcu-micropython#6)
#
# duty() on the legacy 0..1023 scale with its getter, duty_ns() and the duty_ns= keyword
# derived from the frequency, init(freq=, duty_u16=), invert=1, and the getters after a
# runtime set. Registers are read at BREAK checkpoints; the getters print.
#
# Checkpoints:
#   1 -- PWM(Pin(6), freq=1000, duty_u16=32768, invert=1)  -> TCCR0A 0xC3, OCR0A 127
#   2 -- pwm.duty(512)                                     -> OCR0A 127; prints 512
#   3 -- pwm.duty_ns(250000) at 1 kHz (25 %)               -> OCR0A 63; prints 16384
#   4 -- pwm.init(freq=20000, duty_u16=49152)              -> TCCR0B 0x02, OCR0A 191
#   5 -- second PWM(Pin(3), freq=2000, duty_ns=125000)     -> OCR2B 63 (25 % of 500 us)
#        (Pin(3) is Timer2: a second channel of Timer0 after init(freq=20000) would
#        ask the shared prescaler for another bucket and is refused at compile time,
#        PyMCU#300)
#   6 -- pwm.duty_u16(d) with d from GPIOR0 + 8192         -> prints 8192 (seed 0)
#
# Expected UART (115200), seed 0: 512 / 16384 / 8192 / END
from machine import Pin, PWM
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import asm


def main():
    pwm = PWM(Pin(6), freq=1000, duty_u16=32768, invert=1)
    asm("BREAK")
    pwm.duty(512)
    print(pwm.duty())
    asm("BREAK")
    pwm.duty_ns(250000)
    print(pwm.duty_u16())
    asm("BREAK")
    pwm.init(freq=20000, duty_u16=49152)
    asm("BREAK")
    other = PWM(Pin(3), freq=2000, duty_ns=125000)
    asm("BREAK")
    d = 8192 + GPIOR0.value
    pwm.duty_u16(d)
    print(pwm.duty_u16())
    asm("BREAK")
    print("END")
    while True:
        pass


main()
