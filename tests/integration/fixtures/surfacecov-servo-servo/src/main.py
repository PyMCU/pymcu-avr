# Surface coverage: adafruit_motor.servo.Servo (+ _BaseServo members via it).
# Every public member is read or written and the result printed; CPython runs
# the same file under the surfacecov oracle fakes and the two serial streams
# must match line for line.

import board
import pwmio
from adafruit_motor.servo import Servo

pwm = pwmio.PWMOut(board.D9, duty_cycle=0, frequency=50)

print("= Servo() defaults")
s = Servo(pwm)
print(s.angle)              # None: duty_cycle 0 means disabled
print(s.actuation_range)    # 180
print(s.fraction)           # None
print(pwm.frequency)        # 50  (the field _min_duty was derived from)
print(pwm.duty_cycle)       # 0

print("= Servo() keywords")
s2 = Servo(pwm, actuation_range=135)
print(s2.actuation_range)
s3 = Servo(pwm, min_pulse=500)
s4 = Servo(pwm, max_pulse=2500)
s5 = Servo(pwm, actuation_range=90, min_pulse=600, max_pulse=2400)
print(s5.actuation_range)

print("= angle writes")
s.angle = 90
print(s.angle)
print(s.fraction)
print(pwm.duty_cycle)
s.angle = 0
print(s.angle)
print(pwm.duty_cycle)
s.angle = 180
print(s.angle)
print(pwm.duty_cycle)
s.angle = 45.5
print(s.angle)
print(s.fraction)
try:
    s.angle = -1
except ValueError as e:
    print(e)
try:
    s.angle = 181
except ValueError as e:
    print(e)
s.angle = None
print(s.angle)              # None again
print(pwm.duty_cycle)       # 0

print("= fraction writes")
s.fraction = 0.5
print(s.fraction)
print(s.angle)
print(pwm.duty_cycle)
s.fraction = 0.0
print(s.fraction)
print(pwm.duty_cycle)
s.fraction = 1.0
print(s.fraction)
print(pwm.duty_cycle)
s.fraction = 0.1
print(s.fraction)
print(s.angle)
try:
    s.fraction = -0.5
except ValueError as e:
    print(e)
try:
    s.fraction = 1.5
except ValueError as e:
    print(e)
s.fraction = None
print(s.fraction)
print(pwm.duty_cycle)

print("= set_pulse_width_range")
s.set_pulse_width_range()
s.fraction = 1.0
print(s.fraction)
print(pwm.duty_cycle)
s.set_pulse_width_range(500)
print(s.fraction)
s.set_pulse_width_range(500, 2600)
s.fraction = 1.0
print(s.fraction)
print(pwm.duty_cycle)
s.set_pulse_width_range(min_pulse=700, max_pulse=2300)
s.fraction = 0.0
print(s.fraction)
print(pwm.duty_cycle)

print("=DONE=")
