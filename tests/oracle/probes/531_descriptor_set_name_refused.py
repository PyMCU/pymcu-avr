# expect: refuse __set_name__
# doc: docs/language/limitations.md:454
class Slot:
    def __set_name__(self, owner, name):
        self.name = name
    def __get__(self, obj, typ=None):
        return obj.raw + 1
    def __set__(self, obj, value):
        obj.raw = value * 2
class Box:
    value = Slot()
    def __init__(self):
        self.raw = 3
b = Box()
print(b.value)
b.value = 5
print(b.raw)
print("END")
