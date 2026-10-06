# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# sum() over a list literal of run-time elements whose total does not fit a byte. Probe
# 057 sums literals. An accumulator sized from the elements prints 94.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(sum([s + 200, s + 100, s + 50]))
xs = [s + 200, s + 100, s + 50]
print(sum(xs))
print("END")
