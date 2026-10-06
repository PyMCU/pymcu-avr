# expect: match
# doc: https://docs.pymcu.org/roadmap/
class M:
    def __init__(self):
        self.f = 2
    def m(self):
        for i in range(1):
            self.f = self.f + 0.5

m = M()
m.m()
print(m.f)
print("END")
