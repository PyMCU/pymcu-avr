# float-pow: PyMCU/PyMCU#463.
#
# pow() and ** only folded compile-time constant integers. A runtime float raised
# to 2.5 is how CircuitPython applies sRGB gamma (adafruit_tcs34725.py:154):
#   red = int(pow((int((r / clear) * 256) / 255), 2.5) * 255)
# Before this, that line refused with "pow() arguments must be compile-time
# constant integers", so the library did not build.
#
# GPIOR0 is a volatile seed so the constant folder cannot answer. 128 / 256 is
# 0.5; 0.5 ** 2.5 is 0.176776... in IEEE-754 single, and int(that * 1000) is
# 176. The integer unroll x ** 2 is 0.25 -> 250, proving the fractional path
# is the one that ran rather than a multiply being misread as the gamma.
#
# Expected UART output, which is what CPython prints for int((0.5 ** 2.5)*1000)
# and int((0.5 ** 2)*1000) in float32:
#   176
#   176
#   250
#   END
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8


def main() -> None:
    GPIOR0.value = 128
    a: uint8 = GPIOR0.value
    x: float = float(a) / 256.0
    print(int(pow(x, 2.5) * 1000))
    print(int((x ** 2.5) * 1000))
    print(int((x ** 2) * 1000))
    print("END")
    while True:
        pass
