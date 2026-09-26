# expect: refuse next() is part of the iterator protocol
# doc: src/compiler/IR/IRGenerator/Call.cs:1555
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

def g(n):
    yield n + 300
    yield n + 301


it = g(s)
print(next(it))
print("END")
