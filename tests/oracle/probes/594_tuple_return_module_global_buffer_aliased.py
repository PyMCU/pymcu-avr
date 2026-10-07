# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# `global buf` + `return buf` names the module's buffer -- f binds nothing of
# its own -- so a and b must alias the one module array: CPython prints 2 2.
# The module's qualified spelling main.buf is the same object (the replay alias
# proves it), yet the local-binding check counted it as the callee's own
# binding, the resolution declined, and the unpack aliased each target to the
# phantom expansion key inline1.f.buf, which read 0 0.
from pymcu.types import uint8

buf: bytearray = bytearray([9])


def f(n: uint8):
    global buf
    buf[0] = n
    return buf, 0


a, x = f(1)
b, y = f(2)
print(a[0], b[0])
print("END")
