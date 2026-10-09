# expect: refuse attaches a function to the class after its body has already closed
# doc: https://docs.pymcu.org/limitations/
# #426: PyMCU resolves every method call directly, at compile time, to the class
# body's own methods -- there is no dynamic dispatch through a function pointer an
# instance method could be attached to after the class body has closed, the way
# CPython's class objects allow. Refused with a diagnostic instead of silently
# compiling to a call that fails later, undiagnosed, at the actual call site.
class Sensor:
    def __init__(self):
        self.count = 0

def read(self):
    return self.count

Sensor.read = read

s = Sensor()
print(s.read())
print("END")
