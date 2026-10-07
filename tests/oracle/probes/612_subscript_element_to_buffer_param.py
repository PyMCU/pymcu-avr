# expect: refuse reads a whole buffer
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `buf[i]` is ONE ELEMENT, never the buffer itself. Same hole as 611 from the
# other side: before, this silently marshalled the element as a pointer, so
# `takesbuf(buf[0])` on [10, 20, 30] answered 255 instead of raising like
# CPython's `'int' object is not subscriptable`.
def takesbuf(v: bytearray) -> int:
    return v[0]


buf = bytearray([10, 20, 30])
for i in range(3):
    print(takesbuf(buf[i]))
print("END")
