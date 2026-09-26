# expect: refuse 'vars' is runtime reflection
# doc: src/compiler/IR/IRGenerator/Call.cs:1509
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

g = vars()
print(1)
print("END")
