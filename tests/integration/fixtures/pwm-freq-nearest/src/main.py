# ATmega328P: PWM freq argument picks the NEAREST achievable bucket.
#
# PWM("PB1", 128, 1000) on Timer1 fast PWM 8-bit: achievable frequencies are
# 62500/7812.5/976.6/244.1/61.0 Hz. 1000 Hz is 2.4% from 976.6 and 7.8x from
# 7812.5, so the prescaler must be /64: TCCR1B = WGM12 | CS=011 = 0x0B.
# The old above-the-request policy returned /8 (0x0A), 7812 Hz for a 1000 Hz
# request. Verified on a real Uno with a logic analyser: 976.5 Hz measured.
#
# Sends 'F' via UART after setup.
from pymcu.hal.pwm import PWM
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    pwm = PWM("PB1", 128, 1000)
    pwm.start()
    uart.write('F')
    while True:
        delay_ms(1000)
