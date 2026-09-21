# The user's report: "I cannot print the frequency component of adafruit_motor.servo".
# PWMOut(board.D5, frequency=50) on an Uno lands on the 1024-prescaler bucket of a
# 256-count timer, so the pin actually emits 16000000 / (1024 * 256) = 61 Hz -- the
# value `pwm.frequency` must print, in every scope the program reads it from.
import board
import pwmio
from adafruit_motor import servo

pwm = pwmio.PWMOut(board.D5, duty_cycle=0, frequency=50)
s = servo.Servo(pwm)

print(pwm.frequency)
print("f =", pwm.frequency)
print(s._pwm_out.frequency)
f = pwm.frequency
print(f)
print(s._min_duty)
print(s._duty_range)

def show():
    print(pwm.frequency)

show()

class Holder:
    def __init__(self, o):
        self.out = o

h = Holder(pwm)
print(h.out.frequency)

s.angle = 90
print(s._pwm_out.duty_cycle)
print("END")
