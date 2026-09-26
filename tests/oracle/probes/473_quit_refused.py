# expect: refuse quit() is a Python builtin that PyMCU does not provide: there is nothing to quit to
# doc: src/compiler/IR/IRGenerator/Call.cs:6978
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

if s == 1:
    quit()
print(1)
print("END")
