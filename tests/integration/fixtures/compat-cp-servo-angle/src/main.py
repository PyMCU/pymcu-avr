# The servo idiom, whole (pymcu-circuitpython#8).
#
#   pwm = pwmio.PWMOut(board.D9, frequency=50)
#   s = Servo(pwm)
#   s.angle = 90
#
# It did not work. Timer1's eight-bit fast PWM has five frequencies -- 62500, 7812, 976,
# 244 and 61 Hz -- so 50 Hz ran at 61, measured as a 16 384 us period, and PWMOut.frequency
# reported the 50 that was asked for. The duty had 256 counts of 64 us across the whole
# period, which is about 16 steps of angle.
#
# A Timer1 channel asking for a frequency that is not one of the five now reaches mode 14,
# where the period is a register: ICR1 = 39999 counts of 0.5 us is exactly 20 ms, and a
# servo's travel has about 2000 steps.
#
# Read back at each BREAK: GPIOR0/GPIOR1 = OCR1A, GPIOR2 = ICR1 low byte.
import board
import pwmio
from adafruit_motor.servo import Servo
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, OCR1AL, OCR1AH, ICR1L
from pymcu.types import asm, uint8


def report():
    GPIOR0.value = OCR1AL.value
    GPIOR1.value = OCR1AH.value
    GPIOR2.value = ICR1L.value


def main():
    pwm = pwmio.PWMOut(board.D9, frequency=50)
    s = Servo(pwm, min_pulse=1000, max_pulse=2000)

    s.angle = 0
    report()
    asm("BREAK")

    s.angle = 90
    report()
    asm("BREAK")

    s.angle = 180
    report()
    asm("BREAK")

    while True:
        pass
