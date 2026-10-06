# expect: match
# doc: https://docs.pymcu.org/roadmap/
from pymcu.types import outline, uint32

class Base:
    def __init__(self):
        self.v = 70000

class Sub(Base):
    def __init__(self):
        self.own1 = 5
        self.own2 = 6
        try:
            super().__init__()
        except Exception:
            self.v = 0
    @outline
    def total(self) -> uint32:
        return self.own1 + self.own2 + self.v

s = Sub()
print(s.total())
print("END")
