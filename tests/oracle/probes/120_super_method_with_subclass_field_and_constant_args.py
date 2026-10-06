# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
class Base:
    def __init__(self, value):
        self.value = value
    def describe(self):
        return self.value

class Sub(Base):
    def __init__(self, value, extra):
        super().__init__(value)
        self.extra = extra
    def describe(self):
        return super().describe() + self.extra

def report(obj: Sub) -> int:
    return obj.describe()

s = Sub(3, 4)
print(report(s))
print("END")
