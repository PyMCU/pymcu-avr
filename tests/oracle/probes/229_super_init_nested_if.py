# expect: match
# doc: docs/language/roadmap.md
class Base:
    def __init__(self):
        self.tag = "base"
        self.v = 70000

class Sub(Base):
    def __init__(self, external):
        if external:
            super().__init__()
        self.own = 5

s = Sub(True)
print(s.tag)
print(s.v)
print(s.own)
print("END")
