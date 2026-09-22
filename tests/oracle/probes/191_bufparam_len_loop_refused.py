# expect: refuse len() argument must be a fixed-size
# doc: docs/language/roadmap.md
from pymcu.types import uint8, uint16

def fill(buf: bytearray):
    for i in range(len(buf)):
        buf[i] = 0x80 | (i >> 8)

data: uint8[300] = bytearray(300)
fill(data)
print(data[255], data[256], data[299], data[0])
print("END")
