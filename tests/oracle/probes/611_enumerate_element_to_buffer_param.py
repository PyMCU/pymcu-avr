# expect: refuse reads a whole buffer
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `b` from `for i, b in enumerate(buf)` is ONE ELEMENT, never the buffer itself.
# Before, this silently marshalled the element as the pointer a real bytearray
# argument travels as, so the callee's subscript read whatever SRAM byte the
# element's own value happened to address (255, 255, 30 on [10, 20, 30] --
# never an error, never CPython's TypeError).
def takesbuf(v: bytearray) -> int:
    return v[0]


buf = bytearray([10, 20, 30])
for i, b in enumerate(buf):
    print(takesbuf(b))
print("END")
