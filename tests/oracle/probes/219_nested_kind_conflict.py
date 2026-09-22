# expect: match
# doc: docs/language/roadmap.md
# tracked: PyMCU/PyMCU#488
class Rec:
    def __init__(self):
        self.x = 5
    def m(self):
        for i in range(1):
            self.x = "nested"
    def show(self):
        print(self.x)

r = Rec()
r.m()
r.show()
print("END")
