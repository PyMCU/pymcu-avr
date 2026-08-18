# ATmega328P: both channels of one timer stay connected.
#
# TCCRxA is shared by a timer's two channels. pwm_init used to assign it
# absolutely, so initializing OC1B wiped OC1A's COM bits: Arduino's
# analogWrite on D9+D10 together froze D9, confirmed on a real Uno (zero
# edges on D9 while D11 ran). The COM bits are OR-ed in now; after both
# inits TCCR1A must carry COM1A1|COM1B1|WGM10 = 0xA1, and the same on
# Timer0 (0xA3) and Timer2 (0xA3).
#
# Sends 'D' after configuring all six channels.
from pymcu.hal.pwm import PWM
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    a = PWM("PD6", 60)
    b = PWM("PD5", 120)
    c = PWM("PB1", 60)
    d = PWM("PB2", 120)
    e = PWM("PB3", 60)
    f = PWM("PD3", 120)
    a.start()
    b.start()
    c.start()
    d.start()
    e.start()
    f.start()
    uart.write('D')
    while True:
        delay_ms(1000)
