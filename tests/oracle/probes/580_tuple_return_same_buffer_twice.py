# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# `return buf, buf` hands the same object to both slots: a is b in CPython, so
# a[0] = 9 must be visible through b. Copying the callee-local buffer once per
# slot made them two independent objects that answered 1.
from pymcu.types import uint8


def f():
    buf = bytearray(1)
    buf[0] = 1
    return buf, buf


a, b = f()
a[0] = 9
print(b[0])
print("END")
