# Surface coverage: adafruit_motor.servo.ContinuousServo.

import board
import pwmio
from adafruit_motor.servo import ContinuousServo

pwm = pwmio.PWMOut(board.D9, duty_cycle=0, frequency=50)

print("= ContinuousServo() defaults")
c = ContinuousServo(pwm)
print(c.throttle)           # None (disabled)
print(c.fraction)           # None
print(pwm.duty_cycle)

print("= keywords")
c2 = ContinuousServo(pwm, min_pulse=1000)
c3 = ContinuousServo(pwm, max_pulse=2000)
c4 = ContinuousServo(pwm, min_pulse=1100, max_pulse=1900)

print("= throttle writes")
c.throttle = 1.0
print(c.throttle)
print(c.fraction)
print(pwm.duty_cycle)
c.throttle = -1.0
print(c.throttle)
print(c.fraction)
print(pwm.duty_cycle)
c.throttle = 0.0
print(c.throttle)
print(c.fraction)
print(pwm.duty_cycle)
c.throttle = 0.1
print(c.throttle)
try:
    c.throttle = 1.5
except ValueError as e:
    print(e)
try:
    c.throttle = -1.5
except ValueError as e:
    print(e)
c.throttle = None
print(c.throttle)
print(c.fraction)

print("= with-block")
with ContinuousServo(pwm) as cx:
    cx.throttle = 0.5
    print(cx.throttle)
print(pwm.duty_cycle)       # __exit__ set throttle = 0 -> fraction 0.5 duty mid

print("=DONE=")
