# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# abs() with a run-time argument. Probe 121 passes a literal through a one-call-site
# function, so the folder evaluates it. Every value here comes from GPIOR0 and the
# magnitudes do not fit a byte: an abs() that narrowed or read the operand unsigned
# would print 44 or 65236 instead of 300.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: int16 = s - 300
b: int16 = s + 7


def mag(n: int16) -> int16:
    return abs(n)


print(abs(a))
print(abs(b))
print(abs(s - 300))
print(mag(a))
print(mag(s - 2))
x = float(s) - 2.5
print(abs(x))
print("END")
