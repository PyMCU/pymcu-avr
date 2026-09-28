# expect: match
# doc: LANGUAGE_ROADMAP.md:79
# bool() of run-time values written straight into print(). 256 is truthy; a bool()
# that tested only its low byte would answer False -- it must answer True.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(bool(s))
print(bool(s + 5))
print(bool(s + 256))
print("END")
