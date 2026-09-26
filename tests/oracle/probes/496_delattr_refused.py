# expect: refuse 'delattr' is runtime reflection
# doc: docs/language/roadmap.md:253
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

class C:
    def __init__(self, v):
        self.v = v


c = C(s)
delattr(c, "v")
print(1)
print("END")
