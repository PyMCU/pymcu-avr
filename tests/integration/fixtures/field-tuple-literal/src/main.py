# field-tuple-literal: Adafruit DPS310._oversample_scalefactor shape.
#
# A tuple of constants assigned to a field and indexed from a method of the
# imported class. That was refused as "tuples are not supported as runtime
# values".
#
# WHAT DISCRIMINATES: 70. scale[6] is 70. A compile that still refused the
# tuple would not build; one that truncated the table would not print 70.
from pymcu.time import delay_ms
from sensor import Dev

d = Dev()


def main():
    while True:
        print(d.at(6))
        print("END")
        delay_ms(1200)
