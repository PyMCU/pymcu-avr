# PyMCU#430 -- super().method() with a subclass-added field miscomputed when the constructor
# arguments were compile-time constants. Two independent bugs compounded on this exact shape:
#
# 1. EmitUnboundMethodBody allocated its result temp from the base method's declared return
#    type up front. An UNANNOTATED `def describe(self): ...` parses as "void", so no temp was
#    ever allocated, and super().describe() answered None regardless of what the base method
#    actually computed -- VisitReturn's own lazy-allocation fallback mutated a context object
#    this function never read back.
#
# 2. A field literally named "value" (Base's own field here) collides with the compiler's
#    MMIO/pointer `.value` read/write convention. Neither side guarded a receiver whose
#    constructor arguments folded entirely to compile-time constants (no runtime slot ever
#    gets allocated for one), so the store was silently dropped and the read landed on an
#    uninitialized variable instead of the field's real value.
#
# Expected UART output (CPython: Sub(3, 4).describe() = 3 + 4): 7
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Base:
    def __init__(self, value):
        self.value = value

    def describe(self):
        return self.value


class Sub(Base):
    def __init__(self, value, extra):
        super().__init__(value)
        self.extra = extra

    def describe(self):
        return super().describe() + self.extra


def report(obj: Sub) -> int:
    return obj.describe()


uart = UART(9600)
s = Sub(3, 4)
print(report(s))

while True:
    pass
