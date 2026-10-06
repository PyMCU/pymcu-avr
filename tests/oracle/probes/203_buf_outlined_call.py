# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import uint8, outline

class Bus:
    def __init__(self):
        self.last = 0
    @outline
    def writeto(self, addr: uint8, buf: bytearray):
        self.last = buf[0]

class Dev:
    def __init__(self):
        self.temp = bytearray(4)
    def cmd(self, b: Bus):
        self.temp[0] = 64
        b.writeto(60, self.temp)

bus = Bus()
d = Dev()
d.cmd(bus)
print(bus.last)
print("END")
