# class-dict-lookup: Adafruit VEML7700.gain_value shape.
#
# A class-body dict {ALS_GAIN_2: 2, ALS_GAIN_1: 1, ALS_GAIN_X: 0.25} is read
# as self.vals[n] from a method of the imported class. That was refused as
# "Bit index must be constant for reading".
#
# WHAT DISCRIMINATES: 25. vals[2] is 0.25, times 100. A compile that
# still treated the dict as a scalar would not build; one that truncated
# 0.25 to 0 would print 0.
from pymcu.time import delay_ms
from sensor import Dev

d = Dev()


def main():
    while True:
        print(d.scaled(2))
        print("END")
        delay_ms(1200)
