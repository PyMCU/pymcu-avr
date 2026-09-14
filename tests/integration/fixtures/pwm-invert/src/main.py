# PyMCU -- pwm-invert: the inverting compare output mode of the AVR PWM HAL (PyMCU#293)
#
# PWM(pin, duty, freq, invert=1) programs COMxn1:COMxn0 = 11: the pin is set on compare
# match and cleared at BOTTOM, so the duty counts the LOW time. Off (duty 0) still
# disconnects the compare output, and a non-zero duty reconnects it inverting.
#
# Checkpoints (asm BREAK):
#   1 -- PD6 (OC0A) invert=1, duty 128     -> TCCR0A 0xC3, OCR0A 128
#   2 -- set_duty(0)                        -> TCCR0A 0x03 (disconnected)
#   3 -- set_duty(64)                       -> TCCR0A 0xC3, OCR0A 64
#   4 -- PD5 (OC0B) invert=1, duty 200     -> TCCR0A 0xF3 (both channels inverting), OCR0B 200
from pymcu.hal.pwm import PWM
from pymcu.types import asm


def main():
    a = PWM("PD6", 128, 5000, invert=1)
    a.start()
    asm("BREAK")
    a.set_duty(0)
    asm("BREAK")
    a.set_duty(64)
    asm("BREAK")
    b = PWM("PD5", 200, 5000, invert=1)
    b.start()
    asm("BREAK")
    while True:
        pass


main()
