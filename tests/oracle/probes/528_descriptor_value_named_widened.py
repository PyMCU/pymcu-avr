# expect: match
# doc: docs/language/roadmap.md:27
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint16

class Slot:
    def __get__(self, obj, typ=None):
        return obj.raw + 1
    def __set__(self, obj, value):
        obj.raw = value * 2

class Box:
    value = Slot()
    def __init__(self):
        self.raw: uint16 = 300

seed: uint16 = GPIOR0.value
b = Box()
print(b.value)
b.value = 300 + seed
print(b.raw)
print(b.value)
print("END")
