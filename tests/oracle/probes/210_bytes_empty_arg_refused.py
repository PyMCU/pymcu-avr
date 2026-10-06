# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

def touch(buf: bytearray, n: uint8) -> uint8:
    if n == 0:
        return 77
    return buf[0]

print(touch(b"", 0))
print("END")
