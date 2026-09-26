# expect: refuse filter() is a Python builtin that PyMCU does not provide
# doc: docs/language/limitations.md:1059
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

def f(x):
    return x > 1


for v in filter(f, [s, s + 2, s + 3]):
    print(v)
print("END")
