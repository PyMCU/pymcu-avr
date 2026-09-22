# expect: match
# doc: docs/language/roadmap.md
# tracked: PyMCU/PyMCU#487
from pymcu.types import uint8

def head(buf: bytearray) -> uint8:
    return buf[0]

b = bytearray(b"WXYZ")
print(head(memoryview(b)))
print("END")
