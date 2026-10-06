# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# reversed() over a list of run-time elements, one of them wider than a byte. Probe 014
# reverses a literal list. The three values are distinct, so an unreversed walk prints
# 300 first and a narrowed one prints 44.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
xs = [s + 300, s + 5, s + 70]
for v in reversed(xs):
    print(v)
print("END")
