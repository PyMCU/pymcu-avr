# expect: match
# doc: docs/language/roadmap.md
from pymcu.types import uint8

class C:
    def __init__(self):
        self.x = 0
    def m(self):
        for i in range(1):
            self.buf = bytearray(300)
            self.buf[256] = 7

c = C()
c.m()
print(c.buf[256])
print("END")
