# expect: refuse through the class itself
# doc: docs/language/limitations.md:455
class Slot:
    def __get__(self, obj, typ=None):
        if obj is None:
            return -1
        return obj.raw + 1
class Box:
    value = Slot()
    def __init__(self):
        self.raw = 3
print(Box.value)
b = Box()
print(b.value)
print("END")
