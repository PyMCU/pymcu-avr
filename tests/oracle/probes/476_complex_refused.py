# expect: refuse complex() is a Python builtin that PyMCU does not provide: complex numbers
# doc: src/compiler/IR/IRGenerator/Call.cs:6973
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

z = complex(s + 1, 2)
print(1)
print("END")
