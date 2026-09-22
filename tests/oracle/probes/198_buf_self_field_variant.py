# expect: match
# doc: docs/language/roadmap.md
# tracked: PyMCU/PyMCU#487
from pymcu.types import uint8

class Dev:
    def __init__(self):
        self.temp = bytearray(4)

def fill(buf: bytearray, v: uint8):
    buf[0] = v
    buf[1] = v + 1

d = Dev()
fill(d.temp, 40)
print(d.temp[0], d.temp[1])
print("END")
