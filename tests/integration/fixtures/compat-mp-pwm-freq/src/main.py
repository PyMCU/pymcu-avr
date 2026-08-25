# MicroPython machine.PWM integration test fixture — freq parameter
#
# Verifies that:
#   1. machine.PWM(pin, freq=1000) selects prescaler/64 (CS=0x03) for Timer0
#      (nearest achievable: 976 Hz at 2.4% error; the old above-the-request
#      policy returned 7812 Hz, 7.8x off)
#   2. pwm.freq(100) re-selects prescaler/1024 (CS=0x05): 61 Hz is nearer
#      to 100 than 244 Hz is
#
# ATmega328P Timer0 fast-PWM freq = 16MHz / (prescaler * 256):
#   CS=0x01: /1   = 62500 Hz
#   CS=0x02: /8   =  7812 Hz
#   CS=0x03: /64  =   976 Hz
#   CS=0x04: /256 =   244 Hz
#   CS=0x05: /1024=    61 Hz
#
# The duty matters even though this fixture is about frequency: a PWM left at duty
# 0 is off, and off means the compare output is disconnected from the pin, so
# COM0A1 reads back clear. Asking for half duty keeps the channel actually running,
# which is what makes the non-inverting assertion say something.
#
# After setup sends 0x46 ('F') via machine.UART to signal completion.
#
from machine import Pin, PWM, UART


def main():
    uart = UART(0, 9600)
    pwm = PWM(Pin("PD6"), freq=1000)  # nearest bucket 976 Hz -> CS=0x03 (prescaler /64)
    pwm.init()
    pwm.duty(128)                  # 50%: the channel has to be running to be non-inverting
    pwm.freq(100)                  # nearest bucket 61 Hz -> CS=0x05 (prescaler /1024)
    uart.write(0x46)               # 'F' done marker
    while True:
        pass
