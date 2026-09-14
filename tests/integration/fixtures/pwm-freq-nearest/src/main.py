# ATmega328P: the PWM freq argument picks the NEAREST achievable bucket.
#
# PWM("PD6", 128, 1000) on Timer0 fast PWM 8-bit: achievable frequencies are
# 62500/7812.5/976.6/244.1/61.0 Hz. 1000 Hz is 2.4% from 976.6 and 7.8x from
# 7812.5, so the prescaler must be /64: TCCR0B = CS=011 = 0x03. The old
# above-the-request policy returned /8, 7812 Hz for a 1000 Hz request.
#
# This was PB1 (Timer1), and was verified on a real Uno with a logic analyser at
# 976.5 Hz. Timer1 no longer buckets: a frequency that is not one of its five now
# reaches the mode whose period is a register and comes out exactly, which is what
# makes the 50 Hz servo idiom work (pymcu-circuitpython#8). Timer0 and Timer2 still
# bucket, and the rule this fixture is about is still theirs.
#
# Sends 'F' via UART after setup.
from pymcu.hal.pwm import PWM
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


def main():
    uart = UART(9600)
    pwm = PWM("PD6", 128, 1000)
    pwm.start()
    uart.write('F')
    while True:
        delay_ms(1000)
