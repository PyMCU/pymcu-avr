# expect: refuse has no field
# doc: docs/language/roadmap.md
class C:
    def __init__(self):
        self.x = 0
    def m(self):
        for i in range(3):
            self.y = i * 100

c = C()
c.m()
print(c.y)
print("END")
