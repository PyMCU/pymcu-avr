# expect: match
# doc: docs/language/limitations.md
# bin() used to refuse any argument that was not a compile-time constant. A run-time value
# now builds the same spelling into a buffer through pymcu.strfmt, streamed by print().
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(bin(s + 5))
print(bin(s))
print("END")
