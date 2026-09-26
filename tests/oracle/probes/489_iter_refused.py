# expect: refuse iter() is a Python builtin that PyMCU does not provide: there is no iterator protocol
# doc: src/compiler/IR/IRGenerator/Call.cs:6962
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

xs = [s + 300, s + 301]
it = iter(xs)
print(1)
print("END")
