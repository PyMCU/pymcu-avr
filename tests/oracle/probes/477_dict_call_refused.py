# expect: refuse dict() is a Python builtin that PyMCU does not provide: a growable dict needs a heap
# doc: src/compiler/IR/IRGenerator/Call.cs:6957
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

d = dict()
d[s] = 300
print(d[s])
print("END")
