# expect: refuse sorted() is a Python builtin that PyMCU does not provide
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

xs = [s + 300, s + 5, s + 200]
for v in sorted(xs):
    print(v)
print("END")
