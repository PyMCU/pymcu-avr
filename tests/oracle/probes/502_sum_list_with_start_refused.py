# expect: refuse sum() expects exactly one argument
# doc: docs/language/limitations.md:1043
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

xs = [s + 200, s + 100, s + 50]
print(sum(xs, 1000))
print("END")
