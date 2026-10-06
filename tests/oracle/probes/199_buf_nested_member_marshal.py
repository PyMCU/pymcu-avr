# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8

class Inner:
    def __init__(self):
        self.buf = bytearray(4)

class Outer:
    def __init__(self):
        self.dev = Inner()

def fill(buf: bytearray, v: uint8):
    buf[0] = v
    buf[1] = v + 1

o = Outer()
o.dev.buf[3] = 9
fill(o.dev.buf, 40)
print(o.dev.buf[0], o.dev.buf[1], o.dev.buf[3])
print("END")
