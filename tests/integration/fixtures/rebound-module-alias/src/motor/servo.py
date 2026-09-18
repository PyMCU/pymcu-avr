# rebound-module-alias: the Adafruit servo guide spelling
#   from adafruit_motor import servo
#   servo = servo.Servo(pwm)
#   print(servo.fraction)
from pymcu.types import uint8


class Servo:
    def __init__(self) -> None:
        self.x: uint8 = 0

    def get(self) -> uint8:
        return self.x
