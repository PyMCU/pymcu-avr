# expect: refuse has no field
# doc: https://docs.pymcu.org/roadmap/
class C:
    def __init__(self):
        self.x = 0
    def m(self):
        if self.x == 0:
            self.y = 300
        else:
            self.y = 1

c = C()
c.m()
print(c.y)
print("END")
