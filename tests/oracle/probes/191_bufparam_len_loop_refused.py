# expect: match
# doc: https://docs.pymcu.org/roadmap/
# Used to refuse: len(buf) on a plain bytearray parameter had no compile-time answer. It now gets a
# hidden run-time length, since the body measures it (RFC 0015).
from pymcu.types import uint8, uint16

def fill(buf: bytearray):
    for i in range(len(buf)):
        buf[i] = 0x80 | (i >> 8)

data: uint8[300] = bytearray(300)
fill(data)
print(data[255], data[256], data[299], data[0])
print("END")
