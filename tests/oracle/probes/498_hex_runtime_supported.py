# expect: match
# doc: docs/language/limitations.md
# hex() used to refuse any argument that was not a compile-time constant. A run-time value
# now builds the same spelling into a buffer through pymcu.strfmt, streamed by print() --
# the same thing a compile-time constant's flash string streams.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(hex(s + 300))
print(hex(s))
print("END")
