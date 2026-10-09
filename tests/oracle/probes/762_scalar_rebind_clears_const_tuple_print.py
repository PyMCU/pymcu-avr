# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `x = (1, 2)` then `x = 5`: the module pre-scan's tuple record made print
# stream the dead sequence where CPython prints 5. The rebind sweep clears it.
from pymcu.chips.atmega328p import GPIOR0


x = (1, 2)
x = GPIOR0.value + 5
print(x)
print("END")
