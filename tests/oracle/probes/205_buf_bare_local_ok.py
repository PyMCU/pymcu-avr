# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8

def fill(buf: bytearray, v: uint8):
    buf[0] = v
    buf[2] = v + 1

b = bytearray(4)
fill(b, 40)
print(b[0], b[2])
print("END")
