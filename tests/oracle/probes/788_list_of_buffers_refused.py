# expect: refuse a buffer
# doc: https://docs.pymcu.org/limitations/#arithmetic
# `bufs = [buf]`, a list literal whose one element is itself a buffer, has no
# lowering yet -- the element is not constant-foldable, never appended to, and not a
# nested list, so it fell through to a path that copies the element's first BYTE into
# a bufs__0 slot instead of aliasing buf's own address. Refused by name.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
bufs = [buf]
for b in bufs:
    print(head(b))
print("END")
