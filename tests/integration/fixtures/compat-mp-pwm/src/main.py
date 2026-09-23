# MicroPython machine.PWM integration test fixture
#
# Verifies that machine.PWM configures Timer0 Fast PWM on PD6 (OC0A)
# and writes the correct duty cycle to OCR0A.
#
# Expected hardware state after setup:
#   TCCR0A: WGM01|WGM00 = Fast PWM; COM0A1 = non-inverted output
#   OCR0A:  128  (duty_u16(32768): 50% on the upstream rp2 0..65535 scale)
#
# After PWM setup, sends 0x44 ('D') via machine.UART to signal completion.
#
from machine import Pin, PWM, UART


def main():
    uart = UART(0, 9600)
    pwm = PWM(Pin("PD6"))   # PD6 = OC0A
    pwm.init()        # start Fast PWM output on OC0A
    pwm.duty_u16(32768)  # 50% on the 0..65535 scale -> OCR0A = 128
    uart.write(0x44)  # 'D' done marker
    while True:
        pass
