# expect: match
# doc: https://docs.pymcu.org/limitations/
# v = divmod(a, b) (bound to ONE name) used to be a flat compile-time refusal -- PyMCU had
# no general runtime tuple value, only the two-target unpack. It now reads the same
# multi-return-call sentinel any other tuple-returning call answers through.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value
a: uint16 = 200 + s
b: uint16 = 7 + s

v = divmod(a, b)
print(v)
print("END")
