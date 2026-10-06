# expect: match
# doc: https://docs.pymcu.org/roadmap/
class Base:
    def __init__(self):
        self.tag = "base"
        self.v = 70000

class Sub(Base):
    def __init__(self):
        try:
            super().__init__()
        except Exception:
            pass
        self.own = 7

s = Sub()
print(s.tag)
print(s.v)
print(s.own)
print("END")
