# expect: match
# doc: https://docs.pymcu.org/limitations/
# 0.0 ** -1 and pow(0.0, -1) used to raise ValueError (borrowed from math.pow's domain
# check, the shared __pymcu_powf routine's own). CPython's ** and pow() raise
# ZeroDivisionError for this value -- a genuine run-time exception, catchable with
# except ZeroDivisionError -- even for a compile-time-constant operand pair. math.pow
# keeps ValueError, matching CPython's own math.pow domain for the identical value.
import math
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

try:
    print(0.0 ** (-1 - s))
except ZeroDivisionError:
    print("caught-zde-star")

try:
    print(pow(0.0, -1 - s))
except ZeroDivisionError:
    print("caught-zde-pow")

try:
    print(math.pow(0.0, -1 - s))
except ValueError:
    print("caught-ve-mathpow")

try:
    print(pow(0, -1))
except ZeroDivisionError:
    print("caught-zde-int")

print(pow(2.0, -1.0 - float(s)))
print(0.0 ** 2)
print("END")
