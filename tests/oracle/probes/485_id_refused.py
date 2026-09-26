# expect: refuse id() is a Python builtin that PyMCU does not provide: objects have no run-time identity
# doc: src/compiler/IR/IRGenerator/Call.cs:6967
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

x = s
print(id(x) == id(x))
print("END")
