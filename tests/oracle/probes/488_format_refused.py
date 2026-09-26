# expect: refuse format() is a Python builtin that PyMCU does not provide: use an f-string
# doc: src/compiler/IR/IRGenerator/Call.cs:6970
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

print(format(s + 300, "x"))
print("END")
