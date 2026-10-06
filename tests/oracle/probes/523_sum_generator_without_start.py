# expect: match
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# sum(genexp) over run-time elements with no start or with start 0. The optimizer recorded
# `t = 0 + x` as a copy of x and kept forwarding x after the unroll rebound it, so the
# second element was counted where the first belonged ([1, 2, 4, 8] printed 16). The
# explicit additions are the control and come last.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(sum(x for x in [s + 1, s + 2, s + 4, s + 8]))
print(sum((x for x in [s + 1, s + 2, s + 4, s + 8]), 0))
print(sum((x for x in [s + 1, s + 2, s + 4, s + 8]), 1000))
print(sum(x * 2 for x in [s + 200, s + 100]))
print((s + 1) + (s + 2) + (s + 4) + (s + 8))
print("END")
