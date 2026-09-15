# PyMCU -- two-index-unannotated-return: a two-index dunder round trip where neither method
# declares a return type (PyMCU#397).
#
# `def __getitem__(self, key): return ...` has no `-> T`, which is "void" to the parser --
# return-type inference does not run for class methods -- so EmitDunderCall's own result slot
# stayed null. The `return` statement's handler already repairs this for a plain @inline call:
# it allocates a result slot on the fly, sized from the returned value, and stores it back
# onto the (shared) InlineContext. EmitDunderCall never read that back -- it kept returning
# its own stale, never-updated local, still null from before the body ran -- so it fell
# through to a hardcoded Constant(0) regardless of what the body actually computed.
#
# Expected UART output (CPython: m[2, 3] = 4 sets self.value to 2*10+3+4 = 27; m[1, 2] then
# reads 1*10+2+27 = 39 -- the write's own value has to reach a read of a DIFFERENT key):
#   39
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Matrix:
    def __init__(self):
        self.value = 0

    def __setitem__(self, key, value):
        x, y = key
        self.value = x * 10 + y + value

    def __getitem__(self, key):
        x, y = key
        return x * 10 + y + self.value


uart = UART(9600)
m = Matrix()
m[2, 3] = 4
print(m[1, 2])
print("done")

while True:
    pass
