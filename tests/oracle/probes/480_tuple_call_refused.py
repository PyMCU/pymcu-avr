# expect: refuse tuple() is a Python builtin that PyMCU does not provide: building a tuple at run time
# doc: src/compiler/IR/IRGenerator/Call.cs:6959
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

t = tuple([s, s + 300])
print(t[1])
print("END")
