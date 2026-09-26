# expect: match
# doc: LANGUAGE_ROADMAP.md:79
# int() and float() on run-time values. Probe 123 passes literals through one-call-site
# functions and measures the folder. int() must truncate toward zero on both signs (3 and
# -3, where a floor would give -4), and the int-to-float widening must keep 300 whole.
from pymcu.types import int16
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
f = float(s) + 3.9
print(int(f))
g = float(s) - 3.9
print(int(g))
print(float(s + 300))
x = int(s + 300)
print(x)
y: int16 = s - 300
print(int(y))
print("END")
