# expect: refuse exit() is a Python builtin that PyMCU does not provide: there is nothing to exit to
# doc: src/compiler/IR/IRGenerator/Call.cs:6976
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

if s == 1:
    exit()
print(1)
print("END")
