# field-from-method-call -- PyMCU#489 reproducer, kept green.
#
# `self.v = self._read()` where `_read` carries NO return annotation: the
# return-join pass (src/compiler/Frontend/TypeInference.cs) walked only
# prog.Functions, which holds module-level functions, so `return 300` never
# joined a return type. InferAssignedFieldType saw an empty ReturnType where a
# declared one reads as width evidence, the field kept the uint8 default, and
# the outlined Dev__read emitted `LDI R24,44; RET` -- its own return truncated
# to a byte.
#
# The pass now runs over class methods too, so the inferred type lands on the
# FunctionDef exactly as a declared `-> uint16` does and v is uint16. The
# printed answers are CPython's, running the same program:
#   300          the value itself (a byte field truncates the store: 44)
#   300 >> 8 = 1 the tell: a byte field has no high byte
#   300 & 0xFF = 44
#   LoopDev is the same write nested in a for loop (#488 depth), still 300.


class Dev:
    def __init__(self):
        self.v = self._read()

    def _read(self):
        return 300


class LoopDev:
    def __init__(self):
        for i in range(1):
            self.v = self._read()

    def _read(self):
        return 300


d = Dev()
print(d.v)
print(d.v >> 8)
print(d.v & 0xFF)
l = LoopDev()
print(l.v)
