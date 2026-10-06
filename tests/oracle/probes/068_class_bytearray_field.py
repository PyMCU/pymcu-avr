# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Holder:
    def __init__(self, buf):
        self.buf = buf
    def set(self, i, v):
        self.buf[i] = v
buf = bytearray(b"abc")
h = Holder(buf)
h.set(1, 90)
print(buf)
print("END")
