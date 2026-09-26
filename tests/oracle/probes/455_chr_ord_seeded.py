# expect: match
# doc: docs/language/limitations.md:1055
# chr() of a run-time code point written straight into print(), and ord() of a character
# indexed out of a string at a run-time index. Probe 124 folds both. A chr() that lost its
# character prints 66, and an ord() that read the wrong index prints 65 or 67.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(chr(s + 66))
t = "ABC"
print(ord(t[s + 1]))
print("END")
