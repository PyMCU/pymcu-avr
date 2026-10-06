# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

gbuf = bytearray(4)

def fill(buf: bytearray, v: uint8):
    buf[1] = v
    buf[3] = v + 1

fill(gbuf, 30)
print(gbuf[1], gbuf[3])
print("END")
