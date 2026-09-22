# expect: refuse a bytes or list literal has no value
# doc: docs/language/roadmap.md
from pymcu.types import uint8

def touch(buf: bytearray, n: uint8) -> uint8:
    if n == 0:
        return 77
    return buf[0]

print(touch(b"", 0))
print("END")
