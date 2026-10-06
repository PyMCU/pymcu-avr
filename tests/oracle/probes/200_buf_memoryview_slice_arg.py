# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def head(buf: bytearray) -> uint8:
    return buf[0]

b = bytearray(b"WXYZ")
print(head(memoryview(b)))
print("END")
