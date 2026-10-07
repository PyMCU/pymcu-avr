# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# buf: uint8[1] is born inside the callee, so each unpack target is a different
# object in CPython: a keeps 1 after f(2) runs. The annotated spelling filed
# under the caller's f.buf instead of the expansion's inline1.f.buf, so both
# targets aliased the one shared cell and read the last call's write twice.
from pymcu.types import uint8


def f(n: uint8):
    buf: uint8[1] = [n]
    return buf, 0


a, x = f(1)
b, y = f(2)
print(a[0], b[0])
print("END")
