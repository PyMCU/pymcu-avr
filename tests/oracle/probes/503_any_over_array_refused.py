# expect: refuse any() requires a list literal argument
# doc: docs/language/limitations.md:1047
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

a: uint8[3] = [s, s, s + 5]
if any(a):
    print("y")
print("END")
