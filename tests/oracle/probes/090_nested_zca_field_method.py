# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Pin:
    def __init__(self, v):
        self.v = v
    def read(self):
        return self.v + 1
class Wrap:
    def __init__(self):
        self.pin = Pin(8)
    def read(self):
        return self.pin.read()
w = Wrap()
print(w.read())
print("END")
