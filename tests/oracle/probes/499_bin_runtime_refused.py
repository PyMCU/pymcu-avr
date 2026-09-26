# expect: refuse bin() argument must be a compile-time constant integer
# doc: docs/language/limitations.md:1053
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(bin(s + 5))
print("END")
