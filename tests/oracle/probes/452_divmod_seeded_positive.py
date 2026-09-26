# expect: match
# doc: docs/language/limitations.md:1049
# divmod() with run-time operands, both non-negative, the dividend wider than a byte.
# Probe 054 divides two literals and measures the folder. A divmod that divided on eight
# bits would print 3 and 5 (1000 mod 256 is 232) instead of 142 and 6.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
q, r = divmod(s + 1000, s + 7)
print(q)
print(r)
q2, r2 = divmod(s + 300, s + 300)
print(q2)
print(r2)
print("END")
