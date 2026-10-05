# expect: match
# doc: docs/language/limitations.md
# s = hex(v) on a run-time v used to fall to the generic scalar Copy path and take the
# digit buffer's first byte as a plain number -- s = hex(x + 200); print(s) printed "255"
# instead of "0xc8": a silent wrong answer. The assignment now binds the same buffer
# print(hex(v)) builds.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

a = hex(s + 200)
b = bin(s + 2)
c = oct(s + 8)
print(a)
print(b)
print(c)
print("END")
