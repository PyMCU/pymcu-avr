# expect: match
# doc: docs/language/limitations.md:1050
# `**` with a constant exponent of 3 or more and a base that is an expression, not a name.
# The base parked in R16:R17 was lost across the first __mul32 (2295 for 3 ** 3). The same
# base bound to a name first is the control.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
b = s + 3
print(b ** 3)
print((s + 3) ** 2)
print((s + 3) ** 3)
print((s + 1) ** 5)
print((s - 3) ** 3)
print((s + 300) ** 2)
print("END")
