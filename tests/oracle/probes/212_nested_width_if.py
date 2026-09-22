# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

class Cfg:
    def __init__(self):
        self.val = 0
    def apply(self, flag: uint8):
        if flag:
            self.val = 700
        else:
            self.val = 1

c = Cfg()
c.apply(1 - uint8(GPIOR0.value))
print(c.val)
print("END")
