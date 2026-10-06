# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
class Box:
    def __init__(self, value):
        self.value = value

def read(self: Box) -> int:
    return self.value

b = Box(7)
print(read(b))
print("END")
