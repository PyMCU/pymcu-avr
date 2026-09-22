# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8

def head(buf: bytearray) -> uint8:
    return buf[0]

b = bytearray(b"WXYZ")
print(head(b[1:3]))
print("END")
