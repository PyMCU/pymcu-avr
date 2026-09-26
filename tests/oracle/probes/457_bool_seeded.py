# expect: divergence docs/language/type-system.md:20
# doc: LANGUAGE_ROADMAP.md:79
# bool() of run-time values written straight into print(). 256 is truthy but its low byte
# is zero, so a bool() that tested only eight bits prints 0 on the third line.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
print(bool(s))
print(bool(s + 5))
print(bool(s + 256))
print("END")
