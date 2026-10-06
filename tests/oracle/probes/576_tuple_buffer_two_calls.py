# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# Two calls to a function returning (buffer, scalar) give different objects in
# CPython: f's local buf is one cell every call shares, so an unpack target
# aliasing it read the last call's write for both names.
from pymcu.types import uint8


def f(n: uint8):
    buf = bytearray(1)
    buf[0] = n
    return buf, 0


a, x = f(1)
b, y = f(2)
print(a[0], b[0])
print("END")
