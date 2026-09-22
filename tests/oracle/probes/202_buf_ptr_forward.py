# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8

def inner(buf: bytearray, v: uint8):
    buf[0] = v
    buf[1] = v + 1

def outer(buf: bytearray, v: uint8):
    inner(buf, v + 1)
    buf[2] = v + 2

class Dev:
    def __init__(self):
        self.temp = bytearray(4)

d = Dev()
outer(d.temp, 40)
print(d.temp[0], d.temp[1], d.temp[2])
print("END")
