# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8

def head(buf: bytearray) -> uint8:
    return buf[0] + buf[2]

print(head(b"AXC"))
print("END")
