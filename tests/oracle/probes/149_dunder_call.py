# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Box:
    def __init__(self, v):
        self.v = v
    def __call__(self, n):
        return self.v + n
b = Box(5)
print(b(10))
print("END")
