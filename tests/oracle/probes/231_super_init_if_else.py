# expect: match
# doc: https://docs.pymcu.org/roadmap/
class Base:
    def __init__(self):
        self.v = 70000
        self.tag = "base"

class Sub(Base):
    def __init__(self, external):
        if external:
            super().__init__()
        else:
            self.v = 3
            self.tag = "sub"

a = Sub(True)
b = Sub(False)
print(a.v)
print(a.tag)
print(b.v)
print(b.tag)
print("END")
