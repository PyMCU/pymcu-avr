# expect: match
# doc: https://docs.pymcu.org/limitations/
# oct() of a run-time value, the same shape 498/499 cover for hex()/bin().
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(oct(s + 8))
print(oct(s))
print("END")
