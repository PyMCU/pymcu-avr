# PWM(pin) then pwm.freq(f), how servo drivers start, on the Timer1 channel D9: the HAL
# retunes its exact-frequency path (prescaler, ICR1) and the layer keeps duty_u16.
# It was refused at compile time. The second frequency comes from GPIOR0 (0 at reset).
from machine import Pin, PWM
from pymcu.chips.atmega328p import GPIOR0, TCCR1B, ICR1, OCR1A
from pymcu.types import uint16


def regs():
    icr: uint16 = ICR1.value
    ocr: uint16 = OCR1A.value
    print(icr, ocr, TCCR1B.value)


pw = PWM(Pin(9))
pw.duty_u16(32768)
regs()
pw.freq(50)
regs()
fr: uint16 = GPIOR0.value + 20
pw.freq(fr)
regs()
print(pw.freq(), pw.duty_u16())
print("END")
