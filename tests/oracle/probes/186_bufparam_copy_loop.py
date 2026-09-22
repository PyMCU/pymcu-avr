# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8, uint16

def copy(dst: bytearray, src: bytearray, n: uint16):
    for i in range(n):
        dst[i] = src[i]

src: uint8[300] = bytearray(300)
dst: uint8[300] = bytearray(300)
for i in range(300):
    src[i] = 1 + (i >> 8)
copy(dst, src, 300)
print(dst[255], dst[256], dst[299], dst[0])
print("END")
