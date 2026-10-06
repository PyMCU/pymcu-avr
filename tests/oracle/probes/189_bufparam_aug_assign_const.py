# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def bump(buf: bytearray):
    buf[2] += 1
    buf[5] |= 0x10

data = bytearray(8)
data[2] = 40
data[5] = 1
bump(data)
print(data[2], data[5])
print("END")
