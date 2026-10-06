# expect: refuse non-data descriptor
# doc: https://docs.pymcu.org/limitations/#classes-and-inheritance
class Slot:
    def __get__(self, obj, typ=None):
        return obj.raw + 1
class Box:
    value = Slot()
    def __init__(self):
        self.raw = 3
b = Box()
print(b.value)
b.value = 5
print(b.value)
print("END")
