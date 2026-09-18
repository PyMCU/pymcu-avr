# rebound-module-alias: PyMCU/PyMCU#467.
#
# The Adafruit servo guide writes:
#   from adafruit_motor import servo
#   servo = servo.Servo(pwm)
#   print(servo.fraction)
# Assignment rebinds the import alias to an instance. Reads and calls
# used to keep resolving the name as the module.
#
# WHAT DISCRIMINATES:
#   7 -- field read through the rebound name
#   7 -- method call through the rebound name
# Unknown module member: motor_servo_x would not build.
from motor import servo
from pymcu.time import delay_ms


def main():
    while True:
        servo = servo.Servo()
        servo.x = 7
        print(servo.x)
        print(servo.get())
        print("END")
        delay_ms(1200)
