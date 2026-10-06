# expect: refuse 'setattr' is runtime reflection
# doc: https://docs.pymcu.org/roadmap/#not-planned
# CPython runs it; PyMCU must refuse it at compile time with a diagnostic that names the
# builtin, not with a link error, an undefined-function message, or a silent value.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

class C:
    def __init__(self, v):
        self.v = v


c = C(s)
setattr(c, "v", s + 300)
print(c.v)
print("END")
