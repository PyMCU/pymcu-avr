# expect: match
# doc: docs/language/limitations.md
# print(divmod(a, b)) directly, no assignment first -- the other shape 538 covers.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: uint16 = 200 + s
b: uint16 = 7 + s

print(divmod(a, b))
print(divmod(-17 - s, 5))
print("END")
