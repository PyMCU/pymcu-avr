# expect: refuse property() is a Python builtin that PyMCU does not provide
# doc: src/compiler/IR/IRGenerator/Call.cs:1583
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

class P:
    def __init__(self):
        self._v = 300

    def g(self):
        return self._v

    v = property(g)


p = P()
print(p.v)
print("END")
