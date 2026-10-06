# expect: refuse pow() expects exactly two arguments
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(pow(s + 3, s + 200, s + 1000))
print("END")
